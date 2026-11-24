using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Gorecode.Bt.Tests.Golden
{
    /// <summary>Drives a scenario through a runtime and compares the recorded trace with the expected one.</summary>
    public static class GoldenRunner
    {
        const int k_MaxSettleTicks = 1000;

        public static List<List<string>> Run(GoldenScenario scenario, IGoldenRuntime runtime)
        {
            if (scenario.Runners != 1)
            {
                throw new NotSupportedException("Multi-runner scenarios need a runtime with the 'multiRunner' capability.");
            }

            runtime.Load(scenario.Tree, scenario.Trees, scenario.Blackboard, scenario.Looped);

            var updates = new List<List<string>>();
            foreach (var rawStep in scenario.Stimulus)
            {
                var step = rawStep;
                if (step == "tick")
                {
                    TickOnce(runtime, updates);
                }
                else if (step == "settle")
                {
                    if (scenario.Looped)
                    {
                        throw new NotSupportedException("'settle' never terminates in looped execution mode; use explicit ticks");
                    }

                    var guard = 0;
                    while (runtime.HasPendingRequests)
                    {
                        if (++guard > k_MaxSettleTicks)
                        {
                            throw new InvalidOperationException("settle exceeded " + k_MaxSettleTicks + " updates");
                        }

                        TickOnce(runtime, updates);
                    }
                }
                else
                {
                    var segment = new List<string>();
                    runtime.ApplyStep(step, segment);
                    if (segment.Count > 0)
                    {
                        updates.Add(segment);
                    }
                }
            }

            if (runtime.HasPendingRequests && !scenario.PendingAtEnd)
            {
                throw new InvalidOperationException(
                    "scenario '" + scenario.Id + "' ended with a pending execution request; add stimulus or declare \"pendingAtEnd\": true");
            }

            return updates;
        }

        public static bool Matches(GoldenScenario scenario, List<List<string>> actual)
        {
            var expected = scenario.ExpectedUpdates.Select(u => u.ToList()).ToList();
            if (scenario.Flat)
            {
                return expected.SelectMany(x => x).SequenceEqual(actual.SelectMany(x => x));
            }

            return expected.Count == actual.Count
                && expected.Zip(actual, (e, a) => e.SequenceEqual(a)).All(x => x);
        }

        /// <summary>Human-readable side-by-side diff of expected and actual update segments.</summary>
        public static string Describe(GoldenScenario scenario, List<List<string>> actual)
        {
            var sb = new StringBuilder();
            sb.AppendLine("scenario '" + scenario.Id + "' (" + PathOf(scenario) + "), compare=" + (scenario.Flat ? "flat" : "segmented"));
            var expected = scenario.ExpectedUpdates.Select(u => u.ToList()).ToList();
            if (scenario.Flat)
            {
                expected = new List<List<string>> { expected.SelectMany(x => x).ToList() };
                actual = new List<List<string>> { actual.SelectMany(x => x).ToList() };
            }

            var n = Math.Max(expected.Count, actual.Count);
            for (var i = 0; i < n; i++)
            {
                var e = i < expected.Count ? expected[i] : null;
                var a = i < actual.Count ? actual[i] : null;
                var same = e != null && a != null && e.SequenceEqual(a);
                sb.Append("update ").Append(i).Append(same ? " OK      " : " MISMATCH").AppendLine();
                if (e != null)
                {
                    sb.AppendLine("  expected: " + string.Join(", ", e));
                }

                if (a != null)
                {
                    sb.AppendLine("  actual:   " + string.Join(", ", a));
                }

                if (e != null && a != null && !same)
                {
                    foreach (var missing in e.Except(a))
                    {
                        sb.AppendLine("  - missing: " + missing);
                    }

                    foreach (var extra in a.Except(e))
                    {
                        sb.AppendLine("  + extra:   " + extra);
                    }
                }
            }

            return sb.ToString();
        }

        public static string Flatten(List<List<string>> updates)
        {
            return string.Join(" | ", updates.Select(u => string.Join(", ", u)));
        }

        static void TickOnce(IGoldenRuntime runtime, List<List<string>> updates)
        {
            var segment = new List<string>();
            runtime.Tick(segment);
            updates.Add(segment);
        }

        static string PathOf(GoldenScenario s)
        {
            return System.IO.Path.GetFileName(s.Source);
        }

        public static string FormatValue(JToken value)
        {
            switch (value.Type)
            {
                case JTokenType.Boolean:
                    return value.Value<bool>() ? "true" : "false";
                case JTokenType.Integer:
                    return value.Value<long>().ToString(CultureInfo.InvariantCulture);
                case JTokenType.Float:
                    return value.Value<double>().ToString("R", CultureInfo.InvariantCulture);
                default:
                    return value.ToString();
            }
        }
    }
}
