using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace Gorecode.Bt.Tests.Golden
{
    /// <summary>
    /// Golden scenarios (docs/golden-format.md): every scenario is data; expected
    /// traces are never hardcoded in C#. Scenarios whose required capabilities the
    /// runtime lacks are ignored (not failed) so the suite doubles as a coverage
    /// dashboard while the Phase-1 runtime grows.
    /// </summary>
    public class GoldenScenarioTests
    {
        // Structural / bench-gated statements are enforced by other test kinds.
        static readonly string[] k_NoScenarioNeeded = { "S12", "S31", "S38", "S44", "S45" };

        static readonly HashSet<string> k_NodeTypes = new HashSet<string>
        {
            "Selector", "Sequence", "SimpleParallel",
            "BlackboardDecorator", "CooldownDecorator", "LoopDecorator", "TimeLimitDecorator", "ForceResultDecorator",
            "InstantTask", "LatentTask", "Service", "SubtreeTask", "DynamicSubtreeTask",
        };

        static readonly HashSet<string> k_StimulusVerbs = new HashSet<string>
        {
            "start", "stop", "tick", "settle", "write", "clear", "complete", "advance", "inject", "breakpoint", "step", "resume",
        };

        static string RepoRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        static string ScenariosDir => Path.Combine(RepoRoot, "Packages", "com.gorecode.unity-bt", "Tests", "Golden", "Scenarios");
        static string SpecPath => Path.Combine(RepoRoot, "docs", "ue-bt-spec.md");

        public static IEnumerable<TestCaseData> Cases()
        {
            foreach (var s in GoldenScenario.LoadAll(ScenariosDir))
            {
                yield return new TestCaseData(s).SetName("Golden." + s.Id);
            }
        }

        [TestCaseSource(nameof(Cases))]
        public void Scenario_ProducesExpectedTrace(GoldenScenario scenario)
        {
            var runtime = new NaiveReferenceRuntime();
            var missing = scenario.MissingCapabilities(runtime.Capabilities);
            if (missing.Count > 0)
            {
                Assert.Ignore("requires capabilities the naive reference runtime lacks: " + string.Join(", ", missing));
            }

            var actual = GoldenRunner.Run(scenario, runtime);
            Assert.That(GoldenRunner.Matches(scenario, actual), Is.True, GoldenRunner.Describe(scenario, actual));
        }

        [Test]
        public void Scenarios_AreWellFormed()
        {
            var scenarios = GoldenScenario.LoadAll(ScenariosDir);
            Assert.That(scenarios, Is.Not.Empty, "no scenarios found in " + ScenariosDir);

            foreach (var s in scenarios)
            {
                var label = Path.GetFileName(s.Source) + ":" + s.Id;
                Assert.That(s.Id, Is.Not.Null.And.Not.Empty, label + " id");
                Assert.That(s.Title, Is.Not.Null.And.Not.Empty, label + " title");
                Assert.That(s.Statements, Is.Not.Empty, label + " statements");
                Assert.That(s.Tree, Is.Not.Null, label + " tree");

                var nodes = (Newtonsoft.Json.Linq.JObject)s.Tree["nodes"];
                var root = (string)s.Tree["root"];
                Assert.That(nodes.ContainsKey(root), Is.True, label + " root node");
                foreach (var p in nodes.Properties())
                {
                    var type = (string)p.Value["type"];
                    Assert.That(k_NodeTypes.Contains(type), Is.True, label + " node '" + p.Name + "' has unknown type '" + type + "'");
                }

                foreach (var step in s.Stimulus)
                {
                    // Multi-runner scenarios may target a runner with an "@k:" prefix.
                    var body = step.StartsWith("@", StringComparison.Ordinal) ? step.Substring(step.IndexOf(':') + 1) : step;
                    var verb = body.Split(':')[0];
                    Assert.That(k_StimulusVerbs.Contains(verb), Is.True, label + " unknown stimulus step '" + step + "'");
                }

                if (s.Runners == 1)
                {
                    Assert.That(s.ExpectedUpdates, Is.Not.Empty, label + " expected.updates");
                }
                else
                {
                    Assert.That(s.Expected?["runner1"], Is.Not.Null, label + " expected.runner1 missing");
                    Assert.That(s.Expected != null && s.Expected.Count == s.Runners, Is.True, label + " expected must list every runner");
                }
            }
        }

        [Test]
        public void Scenarios_IdsAreUnique()
        {
            var ids = GoldenScenario.LoadAll(ScenariosDir).Select(s => s.Id).ToList();
            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Count), "duplicate scenario ids");
        }

        [Test]
        public void Scenarios_RequireOnlyKnownCapabilities()
        {
            var scenarios = GoldenScenario.LoadAll(ScenariosDir);
            var unknown = scenarios
                .SelectMany(s => s.Requires.Where(r => !GoldenCapabilities.IsKnown(r)).Select(r => Path.GetFileName(s.Source) + ":" + s.Id + " requires '" + r + "'"))
                .ToList();
            Assert.That(unknown, Is.Empty, "unknown capability names (typos would silently skip a scenario forever)");
        }

        [Test]
        public void Scenarios_CoverEveryTraceableSpecStatement()
        {
            if (!File.Exists(SpecPath))
            {
                Assert.Ignore("spec not found at " + SpecPath);
            }

            var spec = File.ReadAllText(SpecPath);
            var statementIds = Regex.Matches(spec, @"\*\*(S\d{2})\s*\[")
                .Cast<Match>().Select(m => m.Groups[1].Value).Distinct().ToList();
            Assert.That(statementIds, Is.Not.Empty, "no statements found in the spec");

            var covered = GoldenScenario.LoadAll(ScenariosDir).SelectMany(s => s.Statements).ToHashSet();
            var missing = statementIds.Where(id => !covered.Contains(id) && !k_NoScenarioNeeded.Contains(id)).ToList();
            Assert.That(missing, Is.Empty, "spec statements without a golden scenario (or an exemption): " + string.Join(", ", missing));
        }

        [Test]
        public void Runner_RejectsTamperedExpectation()
        {
            var s = GoldenScenario.LoadAll(ScenariosDir).First(x => x.Id == "S02-S03-selector-fallback");
            var actual = GoldenRunner.Run(s, new NaiveReferenceRuntime());
            Assert.That(GoldenRunner.Matches(s, actual), Is.True, GoldenRunner.Describe(s, actual));

            var tamperedUpdates = s.ExpectedUpdates.Select(u => u.ToList()).ToList();
            tamperedUpdates[0].Add("bogus.activate");
            var tampered = new GoldenScenario
            {
                Id = s.Id,
                ExpectedUpdates = tamperedUpdates.Select(u => u.ToArray()).ToArray(),
            };
            Assert.That(GoldenRunner.Matches(tampered, actual), Is.False, "harness accepted a wrong expectation");
            StringAssert.Contains("MISMATCH", GoldenRunner.Describe(tampered, actual));
        }
    }
}
