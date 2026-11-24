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
    /// zero-alloc constraints. Its update model follows UE 5.8's component loop as
    /// read from source (docs/golden-format.md, "Update model"): a search only
    /// finds the next task, the current task is then aborted if the search moved
    /// away from it, and at most one task executes per update. Phase 1's runtime
    /// must pass the same scenarios; this class is then deleted.
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
        const int k_Unbounded = int.MaxValue;

        enum St { Idle, Running, Success, Failure }

        /// <summary>Root (start or restart), Continue (a child's result is applied at its parent), BranchStart (an observer asks for one branch, range-limited).</summary>
        enum ReqKind { Root, Continue, BranchStart }

        abstract class N
        {
            public string Id;
            public string Type;
            public N Parent;
            public int Prio;
            /// <summary>Number of nodes in this node's subtree; Prio + Size is the first priority after it.</summary>
            public int Size = 1;
            public St State = St.Idle;
        }

        sealed class Comp : N
        {
            public readonly List<N> Children = new List<N>();
            public int ActiveIndex = k_None;
            public int RepeatChild = k_None;
        }

        sealed class Dec : N
        {
            public N Child;
            public bool AbortSelf, AbortLower, RestartWhenPassing;
            public string Key, Query, Mode, ForcedResult;
            public JToken QueryValue;
            public double Seconds, Timeout, LastExit = -1e9, StartedAt;
            public int Count, Remaining, TimerId = -1;
            /// <summary>True while a time limit is armed for the current relevance period.</summary>
            public bool LimitRelevant;
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
            /// <summary>Exclusive upper bound (pre-order priority) on the task the search may select.</summary>
            public int RangeEnd = k_Unbounded;
            /// <summary>The observer whose evaluation produced this request, if any.</summary>
            public Dec Source;
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
            public readonly List<KeyValuePair<Dec, bool>> ObserverChanges = new List<KeyValuePair<Dec, bool>>();
            public readonly List<Action> Deferred = new List<Action>();
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
        readonly List<Dec> m_Observers = new List<Dec>();
        readonly List<Timer> m_Timers = new List<Timer>();
        Req m_Pending;
        Req m_RequestAfterAbort;
        SearchResult m_Stored;
        Tsk m_ActiveTask;
        Tsk m_AbortingTask;
        bool m_Looped;
        bool m_AbortInProgress;
        double m_Clock;
        int m_NextTimerId;

        /// <summary>Work is pending while a request waits, a task is still aborting, or an outcome waits to be applied.</summary>
        public bool HasPendingRequests => m_Pending != null || m_AbortInProgress || m_Stored != null || m_RequestAfterAbort != null;

        // ---------------------------------------------------------------- loading

        public void Load(JObject tree, JObject namedTrees, IReadOnlyDictionary<string, JToken> blackboard, bool loopedExecution)
        {
            m_Nodes.Clear();
            m_Blackboard.Clear();
            m_Observers.Clear();
            m_Timers.Clear();
            m_Pending = null;
            m_RequestAfterAbort = null;
            m_Stored = null;
            m_ActiveTask = null;
            m_AbortingTask = null;
            m_AbortInProgress = false;
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
            Measure(m_Root);
        }

        static int Measure(N n)
        {
            n.Size = 1;
            if (n is Comp c)
            {
                foreach (var child in c.Children)
                {
                    n.Size += Measure(child);
                }
            }
            else if (n is Dec d)
            {
                n.Size += Measure(d.Child);
            }

            return n.Size;
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

                            d.RestartWhenPassing = (string)def["notify"] == "OnValueChange";
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
                            d.ForcedResult = (string)def["result"];
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
                // A restart forces a root search [src: BehaviorTreeComponent.cpp:472-477].
                // It goes through the request queue so that it waits for a slow abort.
                Queue(new Req { Kind = ReqKind.Root });
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
            // After a slow abort the already-computed outcome is applied without a
            // new search [src: BehaviorTreeComponent.cpp:562-566, 2232-2279].
            if (m_Stored != null && !m_AbortInProgress)
            {
                var stored = m_Stored;
                m_Stored = null;
                Apply(stored, seg);
                return;
            }

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

            if (m_ActiveTask != null)
            {
                // A valid search replaces or restarts the current task: abort it first
                // [src: BehaviorTreeComponent.cpp:2200-2212].
                var current = m_ActiveTask;
                m_ActiveTask = null;
                LeaveBranch(current, sr.Task);
                ResetActiveIndexes(current);
                if (AbortTask(current, seg, false))
                {
                    // Slow abort: the tree keeps its state until the abort completes
                    // [src: BehaviorTreeComponent.cpp:2175-2179].
                    m_AbortInProgress = true;
                    m_Stored = sr;
                    return;
                }
            }

            Apply(sr, seg);
        }

        /// <summary>Applies a valid search: observer bookkeeping, completions, the new path, one task.</summary>
        void Apply(SearchResult sr, List<string> seg)
        {
            // Observers below the new task's position are removed at every task
            // execution [src: BehaviorTreeComponent.cpp:2245-2248], then the search's
            // own observer changes are applied [src: BehaviorTreeComponent.cpp:2253-2254].
            // A search that found nothing registers nobody [src: BehaviorTreeComponent.cpp:1498-1508].
            RemoveObserversBelow(sr.Task);
            Commit(sr.Undo);
            if (sr.Task != null)
            {
                foreach (var change in sr.ObserverChanges)
                {
                    if (change.Value)
                    {
                        if (!m_Observers.Contains(change.Key))
                        {
                            m_Observers.Add(change.Key);
                        }
                    }
                    else
                    {
                        m_Observers.Remove(change.Key);
                    }
                }
            }

            foreach (var completion in sr.Completions)
            {
                if (completion.Node.State == St.Running)
                {
                    E(seg, completion.Node.Id, "complete", completion.Result);
                }

                completion.Node.State = Map(completion.Result);
                completion.Node.ActiveIndex = k_None;
            }

            foreach (var deferred in sr.Deferred)
            {
                deferred();
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
            var first = true;
            var restart = req.Kind == ReqKind.Root;
            Enter(cur, seg, sr);

            while (true)
            {
                int next;
                bool repeated;
                if (first && req.Kind == ReqKind.Continue)
                {
                    // The finished child's result is processed at its parent.
                    last = OnLeaveLink(cur, req.ChildIdx, req.Result, sr);
                    idx = req.ChildIdx;
                    next = NextChild(cur, idx, last, restart, out repeated);
                }
                else if (first && req.Kind == ReqKind.BranchStart)
                {
                    next = req.ChildIdx;
                    repeated = false;
                }
                else
                {
                    next = NextChild(cur, idx, last, restart, out repeated);
                }

                first = false;
                if (next == k_NoChild)
                {
                    // A composite ends with the result that ended it, or with its
                    // default when it ran out of children.
                    var compositeResult = last ?? (cur.Type == "Selector" ? "Failure" : "Success");
                    sr.Completions.Add(new Completion { Node = cur, Result = compositeResult });
                    var parent = LinkOwner(cur);
                    if (parent == null)
                    {
                        sr.Task = null;
                        return sr;
                    }

                    var linkIdx = LinkIndex(cur);
                    last = OnLeaveLink(parent, linkIdx, compositeResult, sr);
                    idx = linkIdx;
                    cur = parent;
                    continue;
                }

                if (!EntryChecks(cur, next, seg))
                {
                    idx = next;
                    last = "Failure";
                    OnLinkBlocked(cur, next, sr);
                    continue;
                }

                OnEnterLink(cur, next, repeated, sr);
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
            }

            // The task must lie inside the requested range [src: BehaviorTreeComponent.cpp:2156-2160],
            // and a running task that refuses restarts makes the search unusable
            // [src: BehaviorTreeComponent.cpp:2162-2172].
            if (sr.Task.Prio >= req.RangeEnd || (sr.Task == m_ActiveTask && sr.Task.KeepRunning))
            {
                sr.Valid = false;
            }

            return sr;
        }

        int NextChild(Comp cur, int lastChildIdx, string lastResult, bool restart, out bool repeated)
        {
            repeated = false;
            if (restart)
            {
                // A restart ignores a pending loop repetition; the loop starts over
                // [src: BTCompositeNode.cpp:603].
                cur.RepeatChild = k_None;
            }

            if (cur.RepeatChild != k_None)
            {
                repeated = true;
                var repeatedChild = cur.RepeatChild;
                cur.RepeatChild = k_None;
                return repeatedChild;
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
                    return false; // attachment order with short-circuit [src: BTCompositeNode.cpp:454-478]
                }
            }

            return true;
        }

        /// <summary>A search enters a child link: observers are armed per abort mode, timers and loop counters start.</summary>
        void OnEnterLink(Comp owner, int childIdx, bool repeated, SearchResult sr)
        {
            foreach (var d in DecoratorChain(owner.Children[childIdx]))
            {
                switch (d.Type)
                {
                    case "BlackboardDecorator":
                        // Self / Both observe while their branch runs; LowerPriority does not
                        // [src: BTCompositeNode.cpp:227-250].
                        if (d.AbortSelf)
                        {
                            sr.ObserverChanges.Add(new KeyValuePair<Dec, bool>(d, true));
                        }

                        if (d.AbortLower && !d.AbortSelf)
                        {
                            sr.ObserverChanges.Add(new KeyValuePair<Dec, bool>(d, false));
                        }

                        break;
                    case "TimeLimitDecorator":
                        // Armed when the search's data is applied — after the abort of the
                        // task the search replaces [src: BehaviorTreeComponent.cpp:2253-2254]
                        // — and only for a new relevance period: a search that re-selects
                        // a task already under this limit leaves the running timer alone
                        // [src: BehaviorTreeComponent.cpp:2056-2062].
                        sr.Deferred.Add(() =>
                        {
                            if (!d.LimitRelevant)
                            {
                                ArmTimeLimit(d);
                            }
                        });
                        break;
                    case "LoopDecorator":
                    {
                        var savedRemaining = d.Remaining;
                        var savedStarted = d.StartedAt;
                        var savedRepeat = owner.RepeatChild;
                        if (!repeated)
                        {
                            d.Remaining = d.Count;
                            d.StartedAt = m_Clock;
                        }

                        d.Remaining--;
                        var again = d.Mode == "Infinite"
                            ? d.Timeout < 0 || m_Clock < d.StartedAt + d.Timeout - 1e-9
                            : d.Remaining > 0;
                        if (again)
                        {
                            owner.RepeatChild = childIdx;
                        }

                        sr.Undo.Add(() => { d.Remaining = savedRemaining; d.StartedAt = savedStarted; owner.RepeatChild = savedRepeat; });
                        break;
                    }
                }
            }
        }

        /// <summary>A child link failed its entry checks: LowerPriority / Both observers arm [src: BTCompositeNode.cpp:297-315].</summary>
        static void OnLinkBlocked(Comp owner, int childIdx, SearchResult sr)
        {
            foreach (var d in DecoratorChain(owner.Children[childIdx]))
            {
                if (d.Type == "BlackboardDecorator" && d.AbortLower)
                {
                    sr.ObserverChanges.Add(new KeyValuePair<Dec, bool>(d, true));
                }
            }
        }

        /// <summary>A child link is left with a result; returns the result after decorator processing.</summary>
        string OnLeaveLink(Comp owner, int childIdx, string result, SearchResult sr)
        {
            foreach (var d in DecoratorChain(owner.Children[childIdx]))
            {
                switch (d.Type)
                {
                    case "ForceResultDecorator":
                        // Converts failure at completion and the failure delivered by a
                        // Self abort [src: BTDecorator_ForceSuccess.cpp:21-43].
                        if (result == "Failure" && d.ForcedResult == "Success")
                        {
                            result = "Success";
                        }

                        break;
                    case "CooldownDecorator":
                    {
                        var savedExit = d.LastExit;
                        d.LastExit = m_Clock;
                        sr.Undo.Add(() => d.LastExit = savedExit);
                        break;
                    }

                    case "TimeLimitDecorator":
                    {
                        var savedTimer = d.TimerId;
                        var savedRelevant = d.LimitRelevant;
                        Disarm(d);
                        d.LimitRelevant = false;
                        sr.Undo.Add(() => { d.TimerId = savedTimer; d.LimitRelevant = savedRelevant; });
                        break;
                    }

                    case "BlackboardDecorator":
                        // Self observers are out of scope once their branch is left;
                        // LowerPriority observers re-arm [src: BTCompositeNode.cpp:281-292].
                        if (d.AbortSelf && !d.AbortLower)
                        {
                            sr.ObserverChanges.Add(new KeyValuePair<Dec, bool>(d, false));
                        }
                        else if (d.AbortLower && !d.AbortSelf)
                        {
                            sr.ObserverChanges.Add(new KeyValuePair<Dec, bool>(d, true));
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
                // The result only schedules the next update [src: BehaviorTreeComponent.cpp:557-561].
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

        /// <summary>Aborts a task; returns true when the abort completes on a later update.</summary>
        bool AbortTask(Tsk t, List<string> seg, bool forceImmediate)
        {
            E(seg, t.Id, "abort");
            Disarm(t.TimerId);
            t.TimerId = -1;
            if (!forceImmediate && t.AbortDelay > 0)
            {
                m_AbortingTask = t;
                AddTimer(m_Clock + t.AbortDelay, s =>
                {
                    E(s, t.Id, "finish", "Aborted");
                    t.State = St.Idle;
                    m_AbortingTask = null;
                    m_AbortInProgress = false;
                    if (m_RequestAfterAbort != null)
                    {
                        // A request that arrived during the abort runs next, with its
                        // range limit removed [src: BehaviorTreeComponent.cpp:1786-1799].
                        m_RequestAfterAbort.RangeEnd = k_Unbounded;
                        m_Pending = m_RequestAfterAbort;
                        m_RequestAfterAbort = null;
                    }
                });
                return true;
            }

            E(seg, t.Id, "finish", "Aborted");
            t.State = St.Idle;
            return false;
        }

        /// <summary>Timed decorators on an abandoned branch stop with it (a time limit re-arms on its next relevance period, S24; a cooldown starts, S22).</summary>
        void LeaveBranch(Tsk t, Tsk newTask)
        {
            for (var p = t.Parent; p != null; p = p.Parent)
            {
                if (p is Dec d)
                {
                    if (d.Type == "TimeLimitDecorator")
                    {
                        // A limit that still encloses the new task keeps running.
                        if (newTask == null || !IsUnder(d, newTask))
                        {
                            Disarm(d);
                            d.LimitRelevant = false;
                        }
                    }
                    else if (d.Type == "CooldownDecorator")
                    {
                        d.LastExit = m_Clock;
                    }
                }
            }
        }

        void ArmTimeLimit(Dec d)
        {
            Disarm(d);
            d.LimitRelevant = true;
            var id = -1;
            id = AddTimer(m_Clock + d.Seconds, s =>
            {
                if (d.TimerId == id)
                {
                    Queue(new Req { Kind = ReqKind.Continue, Start = LinkOwner(d), ChildIdx = LinkIndex(d), Result = "Failure" });
                }
            });
            d.TimerId = id;
        }

        void OnExhausted()
        {
            // The tree finished: every observer is unregistered; looped mode asks for
            // a root search on the next update [src: BehaviorTreeComponent.cpp:585-613].
            m_Observers.Clear();
            foreach (var n in m_Nodes.Values.OfType<Comp>())
            {
                n.State = St.Idle;
                n.ActiveIndex = k_None;
                n.RepeatChild = k_None;
            }

            if (m_Looped)
            {
                m_Pending = new Req { Kind = ReqKind.Root };
            }
        }

        void DoStop(List<string> seg)
        {
            if (m_AbortingTask != null)
            {
                E(seg, m_AbortingTask.Id, "finish", "Aborted");
                m_AbortingTask.State = St.Idle;
                m_AbortingTask = null;
            }

            if (m_ActiveTask != null)
            {
                AbortTask(m_ActiveTask, seg, true);
                m_ActiveTask = null;
            }

            foreach (var n in m_Nodes.Values.OfType<Comp>())
            {
                n.State = St.Idle;
                n.ActiveIndex = k_None;
                n.RepeatChild = k_None;
            }

            m_Timers.Clear();
            m_Observers.Clear();
            m_Pending = null;
            m_RequestAfterAbort = null;
            m_Stored = null;
            m_AbortInProgress = false;
        }

        // ---------------------------------------------------------------- observers

        void Notify(string key, List<string> seg)
        {
            foreach (var d in m_Observers.ToList())
            {
                if (d.Type != "BlackboardDecorator" || d.Key != key)
                {
                    continue;
                }

                // Every notification evaluates the condition; the two notify modes differ
                // only in whether a passing result also re-requests an executing branch
                // [src: BTDecorator_Blackboard.cpp:60-77, BTDecorator.cpp:88-120].
                var pass = Eval(d);
                E(seg, d.Id, "observe");
                E(seg, d.Id, pass ? "pass" : "fail");

                var owner = LinkOwner(d);
                if (owner == null)
                {
                    continue;
                }

                var executing = m_ActiveTask != null && IsUnder(d.Child, m_ActiveTask);
                var branchIdx = LinkIndex(d);
                if (!pass)
                {
                    if (executing)
                    {
                        Queue(new Req { Kind = ReqKind.Continue, Start = owner, ChildIdx = branchIdx, Result = "Failure", Source = d });
                    }
                }
                else
                {
                    // A pass re-requests the branch when it is not executing, when
                    // OnValueChange says so, or when this observer's own stop request is
                    // still waiting — the stop then turns into a restart
                    // [src: BehaviorTreeComponent.cpp:1002, 1018-1022].
                    var ownStopPending = m_Pending != null && m_Pending.Kind == ReqKind.Continue && m_Pending.Source == d;
                    if (!executing || d.RestartWhenPassing || ownStopPending)
                    {
                        Queue(new Req
                        {
                            Kind = ReqKind.BranchStart,
                            Start = owner,
                            ChildIdx = branchIdx,
                            RangeEnd = RangeEndOf(owner, branchIdx),
                            Source = d,
                        });
                    }
                }
            }
        }

        /// <summary>Where the decorated branch ends: the next sibling, or the end of the owning composite's subtree.</summary>
        static int RangeEndOf(Comp owner, int childIdx)
        {
            return childIdx + 1 < owner.Children.Count ? owner.Children[childIdx + 1].Prio : owner.Prio + owner.Size;
        }

        /// <summary>
        /// Keeps the request with the higher-priority search start. A request skipped in
        /// favor of the pending one still widens the pending request's search range
        /// [src: BehaviorTreeComponent.cpp:1256-1281].
        /// </summary>
        void Queue(Req req)
        {
            if (m_AbortInProgress)
            {
                // Nothing is searched while a task is aborting; the request runs when the
                // abort completes and the outcome computed before it is dropped
                // [src: BehaviorTreeComponent.cpp:1432-1454]. When that outcome is "the
                // tree finished", the request is dropped instead
                // [src: BehaviorTreeComponent.cpp:1173-1178].
                if (m_Stored != null)
                {
                    if (m_Stored.Task == null)
                    {
                        return;
                    }

                    Undo(m_Stored.Undo);
                    m_Stored = null;
                }

                if (m_RequestAfterAbort == null || Key(m_RequestAfterAbort) > Key(req))
                {
                    m_RequestAfterAbort = req;
                }

                return;
            }

            if (m_Pending != null && Key(m_Pending) < Key(req))
            {
                m_Pending.RangeEnd = req.RangeEnd == k_Unbounded ? k_Unbounded : Math.Max(m_Pending.RangeEnd, req.RangeEnd);
                return;
            }

            // A request that replaces an older one inherits its range: ranges are widened,
            // never tightened [src: BehaviorTreeComponent.cpp:1409-1416].
            if (m_Pending != null)
            {
                req.RangeEnd = Math.Max(req.RangeEnd, m_Pending.RangeEnd);
            }

            m_Pending = req;
        }

        static long Key(Req req)
        {
            return req.Kind == ReqKind.Root ? long.MinValue : ((long)req.Start.Prio << 20) + Math.Max(req.ChildIdx, 0);
        }

        /// <summary>Removes observers positioned at or below the given task (all of them when there is none).</summary>
        void RemoveObserversBelow(Tsk task)
        {
            if (task == null)
            {
                m_Observers.Clear();
                return;
            }

            var ownLink = new HashSet<Dec>(DecoratorChain(LinkRoot(task)));
            var limit = LinkRoot(task).Prio;
            for (var i = m_Observers.Count - 1; i >= 0; i--)
            {
                if (m_Observers[i].Prio > limit && !ownLink.Contains(m_Observers[i]))
                {
                    m_Observers.RemoveAt(i);
                }
            }
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

        void ResetActiveIndexes(Tsk t)
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

        /// <summary>A search entering a composite activates it: the engine calls the composite's activation hook while searching.</summary>
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
