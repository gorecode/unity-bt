using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Gorecode.Bt.Tests.Golden
{
    /// <summary>One golden scenario loaded from the neutral JSON format (docs/golden-format.md).</summary>
    public sealed class GoldenScenario
    {
        public const string Schema = "unity-bt.golden/1";

        public string Id;
        public string Title;
        public string Source;
        public string DeclaredSchema;
        public string[] Statements = Array.Empty<string>();
        public string[] Requires = Array.Empty<string>();
        public bool Flat;
        public int Runners = 1;
        public JObject Trees;
        public JObject Tree;
        public Dictionary<string, JToken> Blackboard = new Dictionary<string, JToken>();
        public string[] Stimulus = Array.Empty<string>();
        public string[][] ExpectedUpdates = Array.Empty<string[]>();
        public JObject Expected;
        public bool Looped;
        public bool PendingAtEnd;

        public static List<GoldenScenario> LoadAll(string directory)
        {
            var result = new List<GoldenScenario>();
            if (!Directory.Exists(directory))
            {
                return result;
            }

            foreach (var path in Directory.GetFiles(directory, "*.json").OrderBy(p => p, StringComparer.Ordinal))
            {
                result.AddRange(LoadFile(path));
            }

            return result;
        }

        public static IEnumerable<GoldenScenario> LoadFile(string path)
        {
            var root = JToken.Parse(File.ReadAllText(path));
            var list = root is JObject o && o["scenarios"] is JArray arr ? arr : new JArray(root);
            var schema = (string)root["schema"] ?? (string)list.First()?["schema"];
            if (schema != Schema)
            {
                throw new FormatException(Path.GetFileName(path) + ": schema must be '" + Schema + "'");
            }

            foreach (var token in list)
            {
                yield return FromJson((JObject)token, path);
            }
        }

        public static GoldenScenario FromJson(JObject o, string source)
        {
            var s = new GoldenScenario { Source = source };
            s.Id = (string)o["id"];
            s.Title = (string)o["title"];
            s.DeclaredSchema = (string)o["schema"] ?? Schema;
            s.Statements = Strings(o["statements"]);
            s.Requires = Strings(o["requires"]);
            s.Flat = (string)o["compare"] == "flat";
            s.Runners = (int?)o["runners"] ?? 1;
            s.Trees = o["trees"] as JObject;
            s.Tree = o["tree"] as JObject;
            var bb = o["blackboard"] as JObject;
            if (bb != null)
            {
                foreach (var p in bb.Properties())
                {
                    s.Blackboard[p.Name] = p.Value;
                }
            }

            s.Stimulus = Strings(o["stimulus"]);
            s.Looped = (bool?)o["loop"] ?? false;
            s.PendingAtEnd = (bool?)o["pendingAtEnd"] ?? false;
            s.Expected = o["expected"] as JObject;
            var updates = s.Expected?["updates"] as JArray;
            if (updates != null)
            {
                s.ExpectedUpdates = updates.Cast<JArray>().Select(Strings).ToArray();
            }

            return s;
        }

        public List<string> MissingCapabilities(IEnumerable<string> runtimeCapabilities)
        {
            return Requires.Where(r => !runtimeCapabilities.Contains(r)).ToList();
        }

        static string[] Strings(JToken token)
        {
            return token is JArray a ? a.Cast<JValue>().Select(v => (string)v).ToArray() : Array.Empty<string>();
        }
    }
}
