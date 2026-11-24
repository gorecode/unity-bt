using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace Gorecode.Bt.Tests.Golden
{
    /// <summary>
    /// Contract a runtime fulfills to run golden scenarios (docs/golden-format.md).
    /// Phase 1's product runtime implements it; the naive reference runtime in this
    /// test assembly exists only to validate the harness.
    /// </summary>
    public interface IGoldenRuntime
    {
        /// <summary>Capability names the runtime supports (see <see cref="GoldenCapabilities"/>); scenarios requiring a missing one are skipped.</summary>
        IReadOnlyCollection<string> Capabilities { get; }

        /// <summary>Builds the tree (plus named extra trees) and initializes the blackboard instance.</summary>
        void Load(JObject tree, JObject namedTrees, IReadOnlyDictionary<string, JToken> blackboard, bool loopedExecution);

        /// <summary>
        /// Applies a non-tick stimulus step (start, stop, write:, clear:, complete:, advance:,
        /// inject:, breakpoint:, step, resume) and appends the events it produces
        /// synchronously (writes, observer firings, latent finishes) to <paramref name="segment"/>.
        /// </summary>
        void ApplyStep(string step, List<string> segment);

        /// <summary>Runs exactly one update and appends its events to <paramref name="segment"/>.</summary>
        void Tick(List<string> segment);

        /// <summary>True while a deferred execution request is waiting for the next update.</summary>
        bool HasPendingRequests { get; }
    }

    /// <summary>Closed list of capability names; a name outside it in a scenario's "requires" fails the suite.</summary>
    public static class GoldenCapabilities
    {
        public static readonly HashSet<string> Known = new HashSet<string>
        {
            "selector", "sequence", "simpleParallel", "unregisterBranchOnExit",
            "instantTask", "latentTask", "slowAbort", "restartPolicy",
            "blackboard", "blackboardDecorator",
            "observerAbort.self", "observerAbort.lowerPriority", "observerAbort.both", "notify.valueChange",
            "timeAdvance", "cooldown", "loop", "timeLimit", "forceResult", "service",
            "subtree", "dynamicSubtree", "multiRunner", "loopedExecution", "stop",
            "debugger.replay", "debugger.breakpoints",
        };

        public static bool IsKnown(string capability)
        {
            return Known.Contains(capability) || Regex.IsMatch(capability, @"^verify:S\d{2}$");
        }
    }
}
