using System;
using System.Collections.Generic;
using System.Linq;
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
        /// <summary>Capability names the runtime supports; scenarios requiring a missing one are skipped.</summary>
        IReadOnlyCollection<string> Capabilities { get; }

        /// <summary>Builds the tree and initializes the blackboard instance.</summary>
        void Load(JObject tree, IReadOnlyDictionary<string, JToken> blackboard);

        /// <summary>
        /// Applies a non-tick stimulus step (start, stop, write:, clear:, complete:, advance:)
        /// and appends the events it produces synchronously (writes, observer firings,
        /// latent finishes) to <paramref name="segment"/>.
        /// </summary>
        void ApplyStep(string step, List<string> segment);

        /// <summary>Runs exactly one update and appends its events to <paramref name="segment"/>.</summary>
        void Tick(List<string> segment);

        /// <summary>True while a deferred execution request is waiting for the next update.</summary>
        bool HasPendingRequests { get; }
    }
}
