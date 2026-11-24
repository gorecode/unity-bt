using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Gorecode.Bt.Tests.Golden
{
    /// <summary>
    /// Naive reference runtime used only to validate the golden harness (P3).
    /// Not the product runtime: no zero-alloc constraints, no scheduler optimizations.
    /// It follows docs/ue-bt-spec.md v1.0 for the capabilities it declares; Phase 1's
    /// runtime must pass the same scenarios, after which this class is deleted.
    /// </summary>
    public sealed class NaiveReferenceRuntime : IGoldenRuntime
    {
        public IReadOnlyCollection<string> Capabilities => s_Capabilities;

        static readonly string[] s_Capabilities =
        {
            "selector", "sequence", "instantTask", "latentTask", "blackboard",
            "blackboardDecorator", "observerAbort.self", "observerAbort.lowerPriority",
            "observerAbort.both", "restartPolicy", "timeAdvance", "cooldown", "loop",
            "timeLimit", "forceResult", "stop",
        };

        enum St { Idle, Running, Success, Failure, Discarded }

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
            public int ActiveIndex = -1;
        }

        sealed class Dec : N
        {
            public N Child;
            public bool AbortSelf, AbortLower, OnValueChange, LastResult;
            public string Key, Query, Mode, ForceResult;
            public JToken QueryValue;
            public double Seconds, Timeout, LastExit = -1e9, StartedAt;
            public int Count, Iterations, TimerId = -1;
        }

        sealed class Tsk : N
        {
            public string Result = "Success";
            public Dictionary<string, JToken> Writes;
            public double Duration, AbortDelay;
            public int TimerId = -1, Activations;
            public bool KeepRunning, CountActivations;
            public Req PendingContinue;
        }

        abstract class Req { }
        sealed class RootSearchReq : Req { }
        sealed class ContinueReq : Req { public Comp Owner; public int Idx; public string Result; }
        sealed class BranchAbortReq : Req { public Dec D; public bool FailureResult; }
        sealed class LowerAbortReq : Req { public Dec D; }
        sealed class LoopReq : Req { public Dec D; public List<Dec> Chain; public int I; public Comp Owner; public int Idx; }
        sealed class StopReq : Req { }

        sealed class Timer { public int Id; public double Due; public Action<List<string>> Act; }

        N m_Root;
        readonly Dictionary<string, N> m_Nodes = new Dictionary<string, N>();
        readonly Dictionary<string, JToken> m_Blackboard = new Dictionary<string, JToken>();
        readonly List<Dec> m_Observers = new List<Dec>();
        readonly List<Tsk> m_DelayedAborts = new List<Tsk>();
        readonly List<Timer> m_Timers = new List<Timer>();
        List<Req> m_Pending = new List<Req>();
        double m_Clock;
        int m_NextTimerId;

        public bool HasPendingRequests => m_Pending.Count > 0;

        // ---------------------------------------------------------------- loading

        public void Load(JObject tree, IReadOnlyDictionary<string, JToken> blackboard)
        {
            m_Nodes.Clear();
            m_Blackboard.Clear();
            m_Observers.Clear();
            m_Timers.Clear();
            m_Pending = new List<Req>();
            m_Clock = 0;
            m_NextTimerId = 0;

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
            var c = n as Comp;
            if (c != null)
            {
                foreach (var child in c.Children)
                {
                    AssignPrio(child, ref prio);
                }

                return;
            }

            var d = n as Dec;
            if (d != null)
            {
                AssignPrio(d.Child, ref prio);
            }
        }

        // ---------------------------------------------------------------- stimulus

        public void ApplyStep(string step, List<string> seg)
        {
            if (step == "start")
            {
                m_Pending.Add(new RootSearchReq());
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
                    Tsk t;
                    if (!m_Nodes.TryGetValue(parts[0], out var node) || !(node is Tsk))
                    {
                        return;
                    }

                    t = (Tsk)node;
                    if (t.State != St.Running)
                    {
                        return;
                    }

                    E(seg, t.Id, "finish", parts[1]);
                    t.State = parts[1] == "Success" ? St.Success : St.Failure;
                    Disarm(t.TimerId);
                    t.TimerId = -1;
                    m_Pending.Add(new ContinueReq { Owner = LinkOwner(t), Idx = LinkIndex(t), Result = parts[1] });
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
            if (m_Pending.Count == 0)
            {
                return;
            }

            var batch = m_Pending;
            m_Pending = new List<Req>();
            var rootSearch = batch.Any(r => r is RootSearchReq || r is LowerAbortReq);
            var stopped = false;

            // Pass 1: aborts (in request order); a root search in the same batch
            // supersedes every continuation.
            foreach (var r in batch)
            {
                if (r is StopReq)
                {
                    stopped = true;
                }
                else if (r is LowerAbortReq la)
                {
                    AbortLower(la.D, seg);
                }
                else if (r is BranchAbortReq ba)
                {
                    bool delayed;
                    AbortSubtree(ba.D.Child, seg, out delayed);
                    var cont = new ContinueReq
                    {
                        Owner = LinkOwner(ba.D),
                        Idx = LinkIndex(ba.D),
                        Result = ba.FailureResult ? "Failure" : "Aborted",
                    };
                    if (delayed)
                    {
                        foreach (var t in m_DelayedAborts)
                        {
                            t.PendingContinue = cont;
                        }
                    }
                    else if (!rootSearch)
                    {
                        RunContinue(cont, seg);
                    }
                }
            }

            if (stopped)
            {
                DoStop(seg);
                return;
            }

            if (rootSearch)
            {
                RunRootSearch(seg);
                return;
            }

            // Pass 2: deferred continuations and loop iterations.
            foreach (var r in batch)
            {
                if (r is ContinueReq c)
                {
                    RunContinue(c, seg);
                }
                else if (r is LoopReq l)
                {
                    ResumeLoop(l, seg);
                }
            }
        }

        void RunContinue(ContinueReq c, List<string> seg)
        {
            seg.Add("search:" + c.Owner.Id);
            FinishLink(c.Owner, c.Idx, c.Result, seg, false);
        }

        void RunRootSearch(List<string> seg)
        {
            seg.Add("search:root");
            var buf = new List<string>();
            var mark = m_Pending.Count;
            var st = m_Root is Comp ? RunInner(m_Root, buf) : RunLink(m_Root, buf, null);
            if (st == St.Discarded)
            {
                // KeepRunning: the whole search update is rolled back (S49).
                m_Pending.RemoveRange(mark, m_Pending.Count - mark);
                seg.Add("search:discard");
                return;
            }

            seg.AddRange(buf);
        }

        // ---------------------------------------------------------------- execution

        St RunInner(N node, List<string> seg)
        {
            if (node is Comp c)
            {
                var reenter = c.State == St.Running;
                if (!reenter)
                {
                    E(seg, c.Id, "activate");
                    c.State = St.Running;
                    RegisterObservers(c);
                }

                return RunChildAndFinish(c, 0, seg, reenter);
            }

            var t = (Tsk)node;
            if (t.Type == "InstantTask")
            {
                Activate(t, seg);
                E(seg, t.Id, "finish", t.Result);
                return t.Result == "Success" ? St.Success : St.Failure;
            }

            if (t.State == St.Running)
            {
                if (t.KeepRunning)
                {
                    return St.Discarded;
                }

                AbortTask(t, seg, true);
            }

            Activate(t, seg);
            t.State = St.Running;
            if (t.Duration > 0)
            {
                t.TimerId = AddTimer(m_Clock + t.Duration, s => LatentTimerFired(t, s));
            }

            return St.Running;
        }

        void Activate(Tsk t, List<string> seg)
        {
            E(seg, t.Id, "activate");
            if (t.CountActivations)
            {
                t.Activations++;
                E(seg, t.Id, "count", t.Activations.ToString(CultureInfo.InvariantCulture));
            }

            if (t.Writes == null)
            {
                return;
            }

            foreach (var kv in t.Writes)
            {
                Write(kv.Key, kv.Value, seg);
            }
        }

        St RunChildAndFinish(Comp owner, int idx, List<string> seg, bool reenter)
        {
            var child = owner.Children[idx];
            var st = RunLink(child, seg, () =>
            {
                if (reenter && owner.ActiveIndex >= 0 && owner.ActiveIndex != idx)
                {
                    AbortSubtree(owner.Children[owner.ActiveIndex], seg, out _);
                    owner.ActiveIndex = -1;
                }
            });
            if (st == St.Running)
            {
                owner.ActiveIndex = idx;
                return St.Running;
            }

            if (st == St.Discarded)
            {
                return St.Discarded;
            }

            if (reenter && owner.ActiveIndex == idx)
            {
                // The running child no longer passes its entry check: its active
                // nodes are aborted as part of the re-entry.
                AbortSubtree(child, seg, out _);
                owner.ActiveIndex = -1;
            }

            return FinishLink(owner, idx, st == St.Success ? "Success" : "Failure", seg, reenter);
        }

        St RunLink(N node, List<string> seg, Action beforeRun)
        {
            var chain = new List<Dec>();
            for (var n = node; n is Dec; n = ((Dec)n).Child)
            {
                chain.Add((Dec)n);
            }

            var inner = chain.Count > 0 ? chain[chain.Count - 1].Child : node;
            foreach (var d in chain)
            {
                if (!EntryCheck(d, seg))
                {
                    return St.Failure;
                }
            }

            if (beforeRun != null)
            {
                beforeRun();
            }

            return RunInner(inner, seg);
        }

        bool EntryCheck(Dec d, List<string> seg)
        {
            bool r;
            switch (d.Type)
            {
                case "BlackboardDecorator":
                    r = Eval(d);
                    d.LastResult = r;
                    break;
                case "CooldownDecorator":
                    r = m_Clock >= d.LastExit + d.Seconds - 1e-9;
                    break;
                case "TimeLimitDecorator":
                    r = true;
                    if (d.TimerId == -1)
                    {
                        var id = -1;
                        id = AddTimer(m_Clock + d.Seconds, s =>
                        {
                            if (d.TimerId == id)
                            {
                                m_Pending.Add(new BranchAbortReq { D = d, FailureResult = true });
                            }
                        });
                        d.TimerId = id;
                    }

                    break;
                case "LoopDecorator":
                    r = true;
                    d.Iterations = 0;
                    d.StartedAt = m_Clock;
                    break;
                default:
                    r = true;
                    break;
            }

            E(seg, d.Id, r ? "pass" : "fail");
            return r;
        }

        bool ShouldLoop(Dec d)
        {
            if (d.Mode == "Infinite")
            {
                return d.Timeout < 0 || m_Clock < d.StartedAt + d.Timeout - 1e-9;
            }

            return d.Iterations < d.Count;
        }

        St FinishLink(Comp owner, int idx, string result, List<string> seg, bool reenter)
        {
            if (owner.State != St.Running)
            {
                return St.Idle;
            }

            var chain = new List<Dec>();
            for (var n = owner.Children[idx]; n is Dec; n = ((Dec)n).Child)
            {
                chain.Add((Dec)n);
            }

            if (result == "Aborted")
            {
                // Aborted results pass decorators through untouched (S25).
                foreach (var d in chain)
                {
                    if (d.Type == "TimeLimitDecorator")
                    {
                        Disarm(d);
                    }
                }
            }
            else
            {
                for (var i = chain.Count - 1; i >= 0; i--)
                {
                    var d = chain[i];
                    if (d.Type == "LoopDecorator")
                    {
                        d.Iterations++;
                        if (ShouldLoop(d))
                        {
                            owner.ActiveIndex = idx;
                            m_Pending.Add(new LoopReq { D = d, Chain = chain, I = i, Owner = owner, Idx = idx });
                            return St.Running;
                        }
                    }
                    else if (d.Type == "ForceResultDecorator")
                    {
                        if (result == "Failure" && d.ForceResult == "Success")
                        {
                            result = "Success";
                        }
                    }
                    else if (d.Type == "CooldownDecorator")
                    {
                        d.LastExit = m_Clock;
                    }
                    else if (d.Type == "TimeLimitDecorator")
                    {
                        Disarm(d);
                    }
                }
            }

            return AdvanceComposite(owner, idx, result, seg, reenter);
        }

        St AdvanceComposite(Comp owner, int idx, string result, List<string> seg, bool reenter)
        {
            owner.ActiveIndex = -1;
            if (result == "Aborted")
            {
                result = "Failure";
            }

            var isSelector = owner.Type == "Selector";
            if (isSelector ? result == "Success" : result == "Failure")
            {
                return CompleteComposite(owner, result, seg);
            }

            var next = idx + 1;
            if (next >= owner.Children.Count)
            {
                return CompleteComposite(owner, isSelector ? "Failure" : "Success", seg);
            }

            return RunChildAndFinish(owner, next, seg, reenter);
        }

        St CompleteComposite(Comp owner, string result, List<string> seg)
        {
            E(seg, owner.Id, "complete", result);
            owner.State = result == "Success" ? St.Success : St.Failure;
            var linkOwner = LinkOwner(owner);
            if (linkOwner == null)
            {
                return owner.State;
            }

            return FinishLink(linkOwner, LinkIndex(owner), result, seg, false);
        }

        void ResumeLoop(LoopReq l, List<string> seg)
        {
            var st = RunLink(l.D.Child, seg, null);
            if (st != St.Success && st != St.Failure)
            {
                return;
            }

            var result = st == St.Success ? "Success" : "Failure";
            l.D.Iterations++;
            if (ShouldLoop(l.D))
            {
                m_Pending.Add(l);
                return;
            }

            for (var i = l.I - 1; i >= 0; i--)
            {
                var d = l.Chain[i];
                if (d.Type == "ForceResultDecorator")
                {
                    if (result == "Failure" && d.ForceResult == "Success")
                    {
                        result = "Success";
                    }
                }
                else if (d.Type == "CooldownDecorator")
                {
                    d.LastExit = m_Clock;
                }
                else if (d.Type == "TimeLimitDecorator")
                {
                    Disarm(d);
                }
            }

            AdvanceComposite(l.Owner, l.Idx, result, seg, false);
        }

        // ---------------------------------------------------------------- aborts

        void AbortSubtree(N n, List<string> seg, out bool delayed)
        {
            m_DelayedAborts.Clear();
            AbortVisit(n, seg);
            delayed = m_DelayedAborts.Count > 0;
        }

        void AbortVisit(N n, List<string> seg)
        {
            if (n is Tsk t)
            {
                if (t.State == St.Running)
                {
                    AbortTask(t, seg, false);
                }

                return;
            }

            if (n is Comp c)
            {
                if (c.State == St.Running)
                {
                    foreach (var child in c.Children)
                    {
                        AbortVisit(child, seg);
                    }

                    c.State = St.Idle;
                    c.ActiveIndex = -1;
                }

                return;
            }

            var d = (Dec)n;
            AbortVisit(d.Child, seg);
            if (d.Type == "TimeLimitDecorator")
            {
                Disarm(d);
            }
            else if (d.Type == "CooldownDecorator")
            {
                d.LastExit = m_Clock;
            }
            else if (d.Type == "LoopDecorator")
            {
                d.Iterations = 0;
            }
        }

        void AbortTask(Tsk t, List<string> seg, bool immediate)
        {
            E(seg, t.Id, "abort");
            Disarm(t.TimerId);
            t.TimerId = -1;
            if (!immediate && t.AbortDelay > 0)
            {
                t.State = St.Idle;
                m_DelayedAborts.Add(t);
                AddTimer(m_Clock + t.AbortDelay, s =>
                {
                    E(s, t.Id, "finish", "Aborted");
                    if (t.PendingContinue != null)
                    {
                        m_Pending.Add(t.PendingContinue);
                        t.PendingContinue = null;
                    }
                });
                return;
            }

            E(seg, t.Id, "finish", "Aborted");
            t.State = St.Idle;
        }

        void AbortLower(Dec d, List<string> seg)
        {
            foreach (var n in m_Nodes.Values.OrderBy(x => x.Prio).ToList())
            {
                if (n.State != St.Running || n.Prio <= d.Child.Prio)
                {
                    continue;
                }

                if (n is Tsk t)
                {
                    AbortTask(t, seg, true);
                }
                else if (n is Comp c)
                {
                    c.State = St.Idle;
                    c.ActiveIndex = -1;
                }
            }
        }

        void DoStop(List<string> seg)
        {
            foreach (var n in m_Nodes.Values.OrderBy(x => x.Prio).ToList())
            {
                if (n is Tsk t && t.State == St.Running)
                {
                    AbortTask(t, seg, true);
                }
                else if (n is Comp c && c.State == St.Running)
                {
                    c.State = St.Idle;
                    c.ActiveIndex = -1;
                }
            }

            m_Timers.Clear();
            m_Observers.Clear();
            m_Pending.Clear();
        }

        // ---------------------------------------------------------------- blackboard & observers

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

        void Notify(string key, List<string> seg)
        {
            foreach (var d in m_Observers.ToList())
            {
                if (d.Key != key)
                {
                    continue;
                }

                var newResult = Eval(d);
                if (!d.OnValueChange && newResult == d.LastResult)
                {
                    continue; // OnResultChange: re-evaluate only when the result flips
                }

                E(seg, d.Id, "observe");
                E(seg, d.Id, newResult ? "pass" : "fail");
                d.LastResult = newResult;
                if (d.AbortSelf && !newResult && HasActive(d.Child))
                {
                    m_Pending.Add(new BranchAbortReq { D = d });
                }

                if (d.AbortLower && newResult)
                {
                    m_Pending.Add(new LowerAbortReq { D = d });
                }
            }
        }

        void RegisterObservers(Comp c)
        {
            foreach (var child in c.Children)
            {
                for (var n = child; n is Dec; n = ((Dec)n).Child)
                {
                    var d = (Dec)n;
                    if (d.Type == "BlackboardDecorator" && (d.AbortSelf || d.AbortLower) && !m_Observers.Contains(d))
                    {
                        d.LastResult = Eval(d);
                        m_Observers.Add(d);
                    }
                }
            }
        }

        bool HasActive(N n)
        {
            if (n.State == St.Running)
            {
                return true;
            }

            if (n is Dec d)
            {
                return HasActive(d.Child);
            }

            if (n is Comp c)
            {
                return c.Children.Any(HasActive);
            }

            return false;
        }

        // ---------------------------------------------------------------- helpers

        Comp LinkOwner(N n)
        {
            for (var c = n; c.Parent != null; c = c.Parent)
            {
                if (c.Parent is Comp owner)
                {
                    return owner;
                }
            }

            return null;
        }

        int LinkIndex(N n)
        {
            var link = n;
            while (link.Parent != null && !(link.Parent is Comp))
            {
                link = link.Parent;
            }

            return ((Comp)link.Parent).Children.IndexOf(link);
        }

        void LatentTimerFired(Tsk t, List<string> seg)
        {
            if (t.State != St.Running)
            {
                return;
            }

            E(seg, t.Id, "finish", t.Result);
            t.State = t.Result == "Success" ? St.Success : St.Failure;
            t.TimerId = -1;
            m_Pending.Add(new ContinueReq { Owner = LinkOwner(t), Idx = LinkIndex(t), Result = t.Result });
        }

        int AddTimer(double due, Action<List<string>> act)
        {
            var id = m_NextTimerId++;
            m_Timers.Add(new Timer { Id = id, Due = due, Act = act });
            return id;
        }

        void Disarm(int timerId)
        {
            if (timerId < 0)
            {
                return;
            }

            m_Timers.RemoveAll(t => t.Id == timerId);
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
