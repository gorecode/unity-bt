using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Gorecode.Bt.Tests.Golden
{
    /// <summary>
    /// Naive reference runtime used only to validate the golden harness (P3).
    /// Not the product runtime: it trades structure for readability and has no
    /// zero-alloc constraints. Its update model mirrors UE 5.8's component loop as
    /// read from source: a search only finds the next task, then the current task
    /// is aborted if the search moved away, then exactly one task executes
    /// (BehaviorTreeComponent.cpp:2094-2222); every request is deferred to the
    /// next update. Phase 1's runtime must pass the same scenarios; this class is
    /// then deleted.
    /// </summary>
    public sealed class NaiveReferenceRuntime : IGoldenRuntime
    {
        public IReadOnlyCollection<string> Capabilities => s_Capabilities;

        static readonly string[] s_Capabilities =
        {
            "selector", "sequence", "instantTask", "latentTask", "blackboard", "blackboardDecorator",
            "observerAbort.self", "observerAbort.lowerPriority", "observerAbort.both",
            "restartPolicy", "notify.valueChange", "timeAdvance", "cooldown", "loop", "timeLimit",
            "forceResult", "stop", "slowAbort", "loopedExecution",
        };

        const int k_NoChild = -2;
        const int k_None = -1;

        enum St { Idle, Running, Success, Failure }

        enum ReqKind { Root, Continue, BranchStart }

        abstract class N
        {
            public string Id;
            public string Type;
            public N Parent;
            public int Prio;
            public St State = St.Idle;
        }

        sealed class Comp : N
        {
            public readonly List<N> Children = new List<N>();
            public int ActiveIndex = k_None;
            public int OverrideChild = k_None;
        }

        sealed class Dec : N
        {
            public N Child;
            public bool AbortSelf, AbortLower, OnValueChange, LastResult;
            public string Key, Query, Mode, ForceResult;
            public JToken QueryValue;
            public double Seconds, Timeout, LastExit = -1e9, StartedAt;
            public int Count, Remaining, TimerId = -1;
        }

        sealed class Tsk : N
        {
            public string Result = "Success";
            public Dictionary<string, JToken> Writes;
            public double Duration, AbortDelay;
            public int TimerId = -1, Activations;
            public bool KeepRunning, CountActivations;
        }

        sealed class Req
        {
            public ReqKind Kind;
            public Comp Start;
            public int ChildIdx;
            public string Result;
        }

        sealed class Completion
        {
            public Comp Node;
            public string Result;
        }

        sealed class SearchResult
        {
            public Tsk Task;
            public bool Valid = true;
            public readonly List<Comp> Path = new List<Comp>();
            public readonly List<int> PathIdx = new List<int>();
            public readonly List<Completion> Completions = new List<Completion>();
            public readonly List<Action> Undo = new List<Action>();
        }

        sealed class Timer
        {
            public int Id;
            public double Due;
            public Action<List<string>> Act;
        }

        N m_Root;
        Comp m_RootComp;
        readonly Dictionary<string, N> m_Nodes = new Dictionary<string, N>();
        readonly Dictionary<string, JToken> m_Blackboard = new Dictionary<string, JToken>();
        readonly List<Dec> m_Armed = new List<Dec>();
        readonly List<Timer> m_Timers = new List<Timer>();
        Req m_Pending;
        Tsk m_ActiveTask;
        bool m_Looped;
        bool m_AbortInProgress;
        Req m_Deferred;
        double m_Clock;
        int m_NextTimerId;

        public bool HasPendingRequests => m_Pending != null;

        // ---------------------------------------------------------------- loading

        public void Load(JObject tree, JObject namedTrees, IReadOnlyDictionary<string, JToken> blackboard, bool loopedExecution)
        {
            m_Nodes.Clear();
            m_Blackboard.Clear();
            m_Armed.Clear();
            m_Timers.Clear();
            m_Pending = null;
            m_ActiveTask = null;
            m_AbortInProgress = false;
            m_Deferred = null;
            m_Clock = 0;
            m_NextTimerId = 0;
            m_Looped = loopedExecution;

            if (blackboard != null)
            {
                foreach (var kv in blackboard)
                {
                    m_Blackboard[kv.Key] = kv.Value;
                }
            }

            var nodes = (JObject)tree["nodes"];
            var rootId = (string)tree["root"];
            m_Root = Build(rootId, (JObject)nodes[rootId], nodes, null);
            m_RootComp = m_Root as Comp;
            var prio = 0;
            AssignPrio(m_Root, ref prio);
        }

        N Build(string id, JObject def, JObject nodes, N parent)
        {
            var type = (string)def["type"];
            N n;
            switch (type)
            {
                case "Selector":
                case "Sequence":
                {
                    var c = new Comp { Id = id, Type = type, Parent = parent };
                    foreach (var cid in (JArray)def["children"])
                    {
                        var childId = (string)cid;
                        c.Children.Add(Build(childId, (JObject)nodes[childId], nodes, c));
                    }

                    n = c;
                    break;
                }

                case "BlackboardDecorator":
                case "CooldownDecorator":
                case "LoopDecorator":
                case "TimeLimitDecorator":
                case "ForceResultDecorator":
                {
                    var d = new Dec { Id = id, Type = type, Parent = parent };
                    var childId = (string)def["child"];
                    d.Child = Build(childId, (JObject)nodes[childId], nodes, d);
                    switch (type)
                    {
                        case "BlackboardDecorator":
                            d.Key = (string)def["key"];
                            var q = def["query"];
                            if (q.Type == JTokenType.String)
                            {
                                d.Query = (string)q;
                            }
                            else
                            {
                                d.Query = (string)q["op"];
                                d.QueryValue = q["value"];
                            }

                            d.OnValueChange = (string)def["notify"] == "OnValueChange";
                            switch ((string)def["abort"] ?? "None")
                            {
                                case "Self": d.AbortSelf = true; break;
                                case "LowerPriority": d.AbortLower = true; break;
                                case "Both": d.AbortSelf = d.AbortLower = true; break;
                            }

                            break;
                        case "CooldownDecorator":
                            d.Seconds = (double)def["seconds"];
                            break;
                        case "LoopDecorator":
                            d.Mode = (string)def["mode"] ?? "Finite";
                            d.Count = (int?)def["count"] ?? 3;
                            d.Timeout = (double?)def["timeout"] ?? -1;
                            break;
                        case "TimeLimitDecorator":
                            d.Seconds = (double)def["seconds"];
                            break;
                        case "ForceResultDecorator":
                            d.ForceResult = (string)def["result"];
                            break;
                    }

                    n = d;
                    break;
                }

                case "InstantTask":
                case "LatentTask":
                {
                    var t = new Tsk { Id = id, Type = type, Parent = parent };
                    t.Result = (string)def["result"] ?? "Success";
                    var w = def["writes"] as JObject;
                    if (w != null)
                    {
                        t.Writes = new Dictionary<string, JToken>();
                        foreach (var p in w.Properties())
                        {
                            t.Writes[p.Name] = p.Value;
                        }
                    }

                    if (type == "LatentTask")
                    {
                        t.Duration = (double?)def["duration"] ?? 0;
                        t.KeepRunning = (string)def["restartPolicy"] == "KeepRunning";
                        t.AbortDelay = (double?)def["abortDelay"] ?? 0;
                    }

                    t.CountActivations = (bool?)def["countActivations"] ?? false;
                    n = t;
                    break;
                }

                default:
                    throw new NotSupportedException("NaiveReferenceRuntime cannot build node type '" + type + "'");
            }

            m_Nodes[id] = n;
            return n;
        }

        static void AssignPrio(N n, ref int prio)
        {
            n.Prio = prio++;
            if (n is Comp c)
            {
                foreach (var child in c.Children)
                {
                    AssignPrio(child, ref prio);
                }

                return;
            }

            if (n is Dec d)
            {
                AssignPrio(d.Child, ref prio);
            }
        }

        // ---------------------------------------------------------------- stimulus

        public void ApplyStep(string step, List<string> seg)
        {
            if (step == "start")
            {
                // RestartTree: force a root re-evaluation (BehaviorTreeComponent.cpp:472-477).
                m_Pending = new Req { Kind = ReqKind.Root };
                return;
            }

            if (step == "stop")
            {
                DoStop(seg);
                return;
            }

            var colon = step.IndexOf(':');
            if (colon < 0)
            {
                throw new FormatException("Unknown stimulus step '" + step + "'");
            }

            var verb = step.Substring(0, colon);
            var arg = step.Substring(colon + 1);
            switch (verb)
            {
                case "write":
                {
                    var eq = arg.IndexOf('=');
                    if (eq < 0)
                    {
                        throw new FormatException("write step needs key=value: '" + step + "'");
                    }

                    Write(arg.Substring(0, eq), JToken.Parse(arg.Substring(eq + 1)), seg);
                    return;
                }

                case "clear":
                    Clear(arg, seg);
                    return;

                case "complete":
                {
                    var parts = arg.Split(':');
                    if (m_Nodes.TryGetValue(parts[0], out var node) && node is Tsk t)
                    {
                        FinishLatent(t, parts[1], seg);
                    }

                    return;
                }

                case "advance":
                    m_Clock += double.Parse(arg, CultureInfo.InvariantCulture);
                    FireDueTimers(seg);
                    return;
            }

            throw new FormatException("Unknown stimulus step '" + step + "'");
        }

        // ---------------------------------------------------------------- one update

        public void Tick(List<string> seg)
        {
            if (m_Pending == null)
            {
                return;
            }

            var req = m_Pending;
            m_Pending = null;
            seg.Add(req.Kind == ReqKind.Root ? "search:root" : "search:" + req.Start.Id);

            var sr = Search(req, seg);
            if (!sr.Valid)
            {
                Undo(sr.Undo);
                seg.Add("search:discard");
                return;
            }

            // A search that selects a running KeepRunning task was already discarded;
            // any other valid search replaces or restarts the current task.
            if (m_ActiveTask != null)
            {
                var current = m_ActiveTask;
                m_ActiveTask = null;
                UnregisterFrom(sr.Task);
                var delayed = AbortTask(current, seg);
                ResetPathOf(current);
                if (delayed)
                {
                    // Latent abort: the tree keeps its state until the abort finishes,
                    // then the same request is searched again.
                    Undo(sr.Undo);
                    m_AbortInProgress = true;
                    m_Deferred = req;
                    return;
                }
            }

            Commit(sr.Undo);
            foreach (var completion in sr.Completions)
            {
                if (completion.Node.State == St.Running)
                {
                    E(seg, completion.Node.Id, "complete", completion.Result);
                }

                completion.Node.State = Map(completion.Result);
                completion.Node.ActiveIndex = k_None;
            }

            if (sr.Task == null)
            {
                OnExhausted();
                return;
            }

            ResetNotOnPath(sr.Path, sr.Task);
            for (var i = 0; i < sr.Path.Count; i++)
            {
                sr.Path[i].ActiveIndex = sr.PathIdx[i];
            }

            ExecuteTask(sr.Task, seg);
        }

        // ---------------------------------------------------------------- search (finds a task, executes nothing)

        SearchResult Search(Req req, List<string> seg)
        {
            var sr = new SearchResult();
            var cur = req.Kind == ReqKind.Root ? m_RootComp : req.Start;
            if (cur == null || (req.Kind != ReqKind.Root && cur.State != St.Running))
            {
                sr.Task = null;
                return sr;
            }

            var idx = k_None;
            string last = null;
            var bounded = req.Kind == ReqKind.BranchStart;
            var first = true;
            Enter(cur, seg, sr);

            while (true)
            {
                int next;
                bool viaOverride;
                if (first && req.Kind == ReqKind.Continue)
                {
                    // Leaving child req.ChildIdx with req.Result (NotifyDecoratorsOnDeactivation).
                    last = ProcessDeactivation(cur, req.ChildIdx, req.Result, sr.Undo);
                    idx = req.ChildIdx;
                    next = NextChild(cur, idx, last, out viaOverride);
                }
                else if (first && bounded)
                {
                    // The search begins at the decorated branch and is limited to it.
                    next = req.ChildIdx;
                    viaOverride = false;
                }
                else
                {
                    next = NextChild(cur, idx, last, out viaOverride);
                }

                first = false;
                if (next == k_NoChild)
                {
                    // The composite ends with the result that ended it (selector: a child's
                    // success; sequence: a child's failure), or with its default when it ran
                    // out of children.
                    var compositeResult = last ?? (cur.Type == "Selector" ? "Failure" : "Success");
                    sr.Completions.Add(new Completion { Node = cur, Result = compositeResult });
                    var parent = LinkOwner(cur);
                    if (parent == null)
                    {
                        sr.Task = null;
                        return sr;
                    }

                    var linkIdx = LinkIndex(cur);
                    last = ProcessDeactivation(parent, linkIdx, compositeResult, sr.Undo);
                    idx = linkIdx;
                    cur = parent;
                    bounded = false;
                    continue;
                }

                if (!EntryChecks(cur, next, seg))
                {
                    idx = next;
                    last = "Failure";
                    NotifyFailedActivation(cur, next, sr.Undo);
                    if (bounded)
                    {
                        // Nothing executable inside the requested range.
                        sr.Task = null;
                        return sr;
                    }

                    continue;
                }

                NotifyActivation(cur, next, viaOverride, sr.Undo);
                sr.Path.Add(cur);
                sr.PathIdx.Add(next);
                if (Inner(cur.Children[next]) is Tsk task)
                {
                    sr.Task = task;
                    break;
                }

                cur = (Comp)Inner(cur.Children[next]);
                Enter(cur, seg, sr);
                idx = k_None;
                last = null;
                bounded = false;
            }

            // "Task doesn't allow restart and it's already running! Discarding search."
            if (sr.Task == m_ActiveTask && sr.Task.KeepRunning)
            {
                sr.Valid = false;
            }

            return sr;
        }

        int NextChild(Comp cur, int lastChildIdx, string lastResult, out bool viaOverride)
        {
            viaOverride = false;
            if (cur.OverrideChild != k_None)
            {
                viaOverride = true;
                var overridden = cur.OverrideChild;
                cur.OverrideChild = k_None;
                return overridden;
            }

            if (lastChildIdx == k_None)
            {
                return cur.Children.Count > 0 ? 0 : k_NoChild;
            }

            var isSelector = cur.Type == "Selector";
            var succeeded = lastResult == "Success";
            if (isSelector ? succeeded : !succeeded)
            {
                return k_NoChild; // selector done on success, sequence done on failure
            }

            return lastChildIdx + 1 < cur.Children.Count ? lastChildIdx + 1 : k_NoChild;
        }

        bool EntryChecks(Comp owner, int childIdx, List<string> seg)
        {
            foreach (var d in DecoratorChain(owner.Children[childIdx]))
            {
                bool pass;
                switch (d.Type)
                {
                    case "BlackboardDecorator":
                        pass = Eval(d);
                        d.LastResult = pass;
                        break;
                    case "CooldownDecorator":
                        pass = m_Clock >= d.LastExit + d.Seconds - 1e-9;
                        break;
                    default:
                        pass = true;
                        break;
                }

                E(seg, d.Id, pass ? "pass" : "fail");
                if (!pass)
                {
                    return false; // attachment order with short-circuit (BTCompositeNode.cpp:454-478)
                }
            }

            return true;
        }

        void NotifyActivation(Comp owner, int childIdx, bool viaOverride, List<Action> undo)
        {
            foreach (var d in DecoratorChain(owner.Children[childIdx]))
            {
                switch (d.Type)
                {
                    case "BlackboardDecorator":
                        if (d.AbortSelf)
                        {
                            Arm(d, undo); // Self / Both
                        }

                        if (d.AbortLower && !d.AbortSelf)
                        {
                            Disarm(d, undo); // LowerPriority
                        }

                        break;
                    case "TimeLimitDecorator":
                    {
                        var saved = d.TimerId;
                        var id = -1;
                        id = AddTimer(m_Clock + d.Seconds, s =>
                        {
                            if (d.TimerId == id)
                            {
                                Queue(new Req { Kind = ReqKind.Continue, Start = LinkOwner(d), ChildIdx = LinkIndex(d), Result = "Failure" });
                            }
                        });
                        d.TimerId = id;
                        undo.Add(() => { Disarm(id); d.TimerId = saved; });
                        break;
                    }

                    case "LoopDecorator":
                    {
                        var savedRemaining = d.Remaining;
                        var savedStarted = d.StartedAt;
                        var savedOverride = owner.OverrideChild;
                        if (!viaOverride)
                        {
                            d.Remaining = d.Count;
                            d.StartedAt = m_Clock;
                        }

                        d.Remaining--;
                        var shouldLoop = d.Mode == "Infinite"
                            ? d.Timeout < 0 || m_Clock < d.StartedAt + d.Timeout - 1e-9
                            : d.Remaining > 0;
                        if (shouldLoop)
                        {
                            owner.OverrideChild = childIdx;
                        }

                        undo.Add(() => { d.Remaining = savedRemaining; d.StartedAt = savedStarted; owner.OverrideChild = savedOverride; });
                        break;
                    }
                }
            }
        }

        void NotifyFailedActivation(Comp owner, int childIdx, List<Action> undo)
        {
            foreach (var d in DecoratorChain(owner.Children[childIdx]))
            {
                if (d.Type == "BlackboardDecorator" && d.AbortLower)
                {
                    Arm(d, undo); // LowerPriority / Both
                }
            }
        }

        /// <summary>Regular deactivation of a child link; returns the result after decorator processing.</summary>
        string ProcessDeactivation(Comp owner, int childIdx, string result, List<Action> undo)
        {
            foreach (var d in DecoratorChain(owner.Children[childIdx]))
            {
                switch (d.Type)
                {
                    case "ForceResultDecorator":
                        if (result == "Failure" && d.ForceResult == "Success")
                        {
                            result = "Success";
                        }

                        break;
                    case "CooldownDecorator":
                    {
                        var saved = d.LastExit;
                        d.LastExit = m_Clock;
                        undo.Add(() => d.LastExit = saved);
                        break;
                    }

                    case "TimeLimitDecorator":
                    {
                        var saved = d.TimerId;
                        Disarm(d);
                        undo.Add(() => d.TimerId = saved);
                        break;
                    }

                    case "BlackboardDecorator":
                        if (d.AbortSelf && !d.AbortLower)
                        {
                            Disarm(d, undo); // Self observers are out of scope once the branch is left
                        }
                        else if (d.AbortLower && !d.AbortSelf)
                        {
                            Arm(d, undo); // LowerPriority observers re-arm when their branch is left
                        }

                        break;
                }
            }

            return result;
        }

        // ---------------------------------------------------------------- execution

        void ExecuteTask(Tsk t, List<string> seg)
        {
            E(seg, t.Id, "activate");
            if (t.CountActivations)
            {
                t.Activations++;
                E(seg, t.Id, "count", t.Activations.ToString(CultureInfo.InvariantCulture));
            }

            if (t.Writes != null)
            {
                foreach (var kv in t.Writes)
                {
                    Write(kv.Key, kv.Value, seg);
                }
            }

            if (t.Type == "InstantTask")
            {
                E(seg, t.Id, "finish", t.Result);
                t.State = Map(t.Result);
                Queue(new Req { Kind = ReqKind.Continue, Start = LinkOwner(t), ChildIdx = LinkIndex(t), Result = t.Result });
                return;
            }

            t.State = St.Running;
            m_ActiveTask = t;
            if (t.Duration > 0)
            {
                var id = -1;
                id = AddTimer(m_Clock + t.Duration, s => FinishLatent(t, t.Result, s));
                t.TimerId = id;
            }
        }

        void FinishLatent(Tsk t, string result, List<string> seg)
        {
            if (m_ActiveTask != t)
            {
                return;
            }

            E(seg, t.Id, "finish", result);
            t.State = Map(result);
            m_ActiveTask = null;
            Disarm(t.TimerId);
            t.TimerId = -1;
            Queue(new Req { Kind = ReqKind.Continue, Start = LinkOwner(t), ChildIdx = LinkIndex(t), Result = result });
        }

        /// <summary>Aborts a task; returns true when the abort is latent (completes later).</summary>
        bool AbortTask(Tsk t, List<string> seg)
        {
            E(seg, t.Id, "abort");
            Disarm(t.TimerId);
            t.TimerId = -1;
            if (t.AbortDelay > 0)
            {
                AddTimer(m_Clock + t.AbortDelay, s =>
                {
                    E(s, t.Id, "finish", "Aborted");
                    t.State = St.Idle;
                    m_AbortInProgress = false;
                    if (m_Deferred != null)
                    {
                        m_Pending = m_Deferred;
                        m_Deferred = null;
                    }
                });
                return true;
            }

            E(seg, t.Id, "finish", "Aborted");
            t.State = St.Idle;
            return false;
        }

        void OnExhausted()
        {
            // Ran out of nodes: every auxiliary node is unregistered; looped mode
            // re-requests a root search for the next update (BehaviorTreeComponent.cpp:585-613).
            m_Armed.Clear();
            foreach (var n in m_Nodes.Values.OfType<Comp>())
            {
                n.State = St.Idle;
                n.ActiveIndex = k_None;
                n.OverrideChild = k_None;
            }

            if (m_Looped)
            {
                m_Pending = new Req { Kind = ReqKind.Root };
            }
        }

        void DoStop(List<string> seg)
        {
            if (m_ActiveTask != null)
            {
                AbortTask(m_ActiveTask, seg);
                m_ActiveTask = null;
            }

            foreach (var n in m_Nodes.Values.OfType<Comp>())
            {
                n.State = St.Idle;
                n.ActiveIndex = k_None;
                n.OverrideChild = k_None;
            }

            m_Timers.Clear();
            m_Armed.Clear();
            m_Pending = null;
            m_Deferred = null;
            m_AbortInProgress = false;
        }

        // ---------------------------------------------------------------- observers

        void Notify(string key, List<string> seg)
        {
            foreach (var d in m_Armed.ToList())
            {
                if (d.Type != "BlackboardDecorator" || d.Key != key)
                {
                    continue;
                }

                var pass = Eval(d);
                if (!d.OnValueChange && pass == d.LastResult)
                {
                    continue; // OnResultChange: request only when the result flips
                }

                E(seg, d.Id, "observe");
                E(seg, d.Id, pass ? "pass" : "fail");
                d.LastResult = pass;

                var owner = LinkOwner(d);
                if (owner == null)
                {
                    continue;
                }

                var executing = m_ActiveTask != null && IsUnder(d.Child, m_ActiveTask);
                if (!pass)
                {
                    if (executing)
                    {
                        // RequestBranchDeactivation: the parent continues with a failed child.
                        Queue(new Req { Kind = ReqKind.Continue, Start = owner, ChildIdx = LinkIndex(d), Result = "Failure" });
                    }
                }
                else if (!executing || d.OnValueChange)
                {
                    // RequestBranchActivation: a search limited to the decorated branch; with
                    // OnValueChange it also restarts an executing branch.
                    Queue(new Req { Kind = ReqKind.BranchStart, Start = owner, ChildIdx = LinkIndex(d) });
                }
            }
        }

        /// <summary>A pending request of higher priority wins; the new one is skipped (BehaviorTreeComponent.cpp:1256-1281).</summary>
        void Queue(Req req)
        {
            if (m_Pending != null && Key(m_Pending) < Key(req))
            {
                return;
            }

            m_Pending = req;
        }

        static long Key(Req req)
        {
            return req.Kind == ReqKind.Root ? long.MinValue : ((long)req.Start.Prio << 20) + Math.Max(req.ChildIdx, 0);
        }

        /// <summary>Unregisters observers at or below the new task's position (UnregisterAuxNodesUpTo).</summary>
        void UnregisterFrom(Tsk newTask)
        {
            if (newTask == null)
            {
                m_Armed.Clear();
                return;
            }

            var keep = new HashSet<Dec>(DecoratorChain(LinkRoot(newTask)));
            var limit = LinkRoot(newTask).Prio;
            for (var i = m_Armed.Count - 1; i >= 0; i--)
            {
                if (m_Armed[i].Prio > limit && !keep.Contains(m_Armed[i]))
                {
                    m_Armed.RemoveAt(i);
                }
            }
        }

        void Arm(Dec d, List<Action> undo)
        {
            if (m_Armed.Contains(d))
            {
                return;
            }

            m_Armed.Add(d);
            undo.Add(() => m_Armed.Remove(d));
        }

        void Disarm(Dec d, List<Action> undo)
        {
            var index = m_Armed.IndexOf(d);
            if (index < 0)
            {
                return;
            }

            m_Armed.RemoveAt(index);
            undo.Add(() => m_Armed.Insert(Math.Min(index, m_Armed.Count), d));
        }

        // ---------------------------------------------------------------- blackboard

        bool Eval(Dec d)
        {
            switch (d.Query)
            {
                case "IsSet": return m_Blackboard.ContainsKey(d.Key);
                case "IsNotSet": return !m_Blackboard.ContainsKey(d.Key);
            }

            if (!m_Blackboard.TryGetValue(d.Key, out var v))
            {
                return false;
            }

            switch (d.Query)
            {
                case "eq": return JToken.DeepEquals(v, d.QueryValue);
                case "ne": return !JToken.DeepEquals(v, d.QueryValue);
            }

            if (!TryNum(v, out var a) || !TryNum(d.QueryValue, out var b))
            {
                return false;
            }

            switch (d.Query)
            {
                case "gt": return a > b;
                case "ge": return a >= b;
                case "lt": return a < b;
                case "le": return a <= b;
                default: return false;
            }
        }

        static bool TryNum(JToken t, out double v)
        {
            switch (t?.Type)
            {
                case JTokenType.Integer: v = t.Value<long>(); return true;
                case JTokenType.Float: v = t.Value<double>(); return true;
                default: v = 0; return false;
            }
        }

        void Write(string key, JToken value, List<string> seg)
        {
            var changed = !m_Blackboard.TryGetValue(key, out var old) || !JToken.DeepEquals(old, value);
            m_Blackboard[key] = value;
            E(seg, "bb", "write", key + "=" + GoldenRunner.FormatValue(value));
            if (changed)
            {
                Notify(key, seg);
            }
        }

        void Clear(string key, List<string> seg)
        {
            var had = m_Blackboard.Remove(key);
            E(seg, "bb", "clear", key);
            if (had)
            {
                Notify(key, seg);
            }
        }

        // ---------------------------------------------------------------- helpers

        static List<Dec> DecoratorChain(N link)
        {
            var chain = new List<Dec>();
            for (var n = link; n is Dec; n = ((Dec)n).Child)
            {
                chain.Add((Dec)n);
            }

            return chain;
        }

        static N Inner(N link)
        {
            var n = link;
            while (n is Dec d)
            {
                n = d.Child;
            }

            return n;
        }

        static N LinkRoot(N n)
        {
            var link = n;
            while (link.Parent != null && !(link.Parent is Comp))
            {
                link = link.Parent;
            }

            return link;
        }

        static Comp LinkOwner(N n)
        {
            return LinkRoot(n).Parent as Comp;
        }

        static int LinkIndex(N n)
        {
            var link = LinkRoot(n);
            return ((Comp)link.Parent).Children.IndexOf(link);
        }

        void ResetPathOf(Tsk t)
        {
            for (var n = t.Parent; n != null; n = n.Parent)
            {
                if (n is Comp c)
                {
                    c.ActiveIndex = k_None;
                }
            }
        }

        void ResetNotOnPath(List<Comp> path, Tsk task)
        {
            var keep = new HashSet<Comp>(path);
            for (var n = (N)task; n != null; n = n.Parent)
            {
                if (n is Comp ancestor)
                {
                    keep.Add(ancestor);
                }
            }

            foreach (var c in m_Nodes.Values.OfType<Comp>())
            {
                if (c.State == St.Running && !keep.Contains(c))
                {
                    c.State = St.Idle;
                    c.ActiveIndex = k_None;
                }
            }
        }

        /// <summary>A search entering a composite activates it: UE calls OnNodeActivation while searching.</summary>
        static void Enter(Comp c, List<string> seg, SearchResult sr)
        {
            if (c.State == St.Running)
            {
                return;
            }

            E(seg, c.Id, "activate");
            c.State = St.Running;
            sr.Undo.Add(() => c.State = St.Idle);
        }

        static bool IsUnder(N ancestor, N node)
        {
            for (var n = node; n != null; n = n.Parent)
            {
                if (n == ancestor)
                {
                    return true;
                }
            }

            return false;
        }

        static St Map(string result)
        {
            return result == "Success" ? St.Success : St.Failure;
        }

        static void Undo(List<Action> undo)
        {
            for (var i = undo.Count - 1; i >= 0; i--)
            {
                undo[i]();
            }
        }

        static void Commit(List<Action> undo)
        {
            undo.Clear();
        }

        int AddTimer(double due, Action<List<string>> act)
        {
            var id = m_NextTimerId++;
            m_Timers.Add(new Timer { Id = id, Due = due, Act = act });
            return id;
        }

        void Disarm(int timerId)
        {
            if (timerId >= 0)
            {
                m_Timers.RemoveAll(t => t.Id == timerId);
            }
        }

        void Disarm(Dec d)
        {
            Disarm(d.TimerId);
            d.TimerId = -1;
        }

        void FireDueTimers(List<string> seg)
        {
            while (true)
            {
                Timer next = null;
                foreach (var t in m_Timers)
                {
                    if (t.Due <= m_Clock + 1e-9 && (next == null || t.Due < next.Due))
                    {
                        next = t;
                    }
                }

                if (next == null)
                {
                    return;
                }

                m_Timers.Remove(next);
                next.Act(seg);
            }
        }

        static void E(List<string> seg, string node, string verb, string arg = null)
        {
            seg.Add(arg == null ? node + "." + verb : node + "." + verb + ":" + arg);
        }
    }
}
