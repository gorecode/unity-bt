using System;
using System.Linq;
using System.Reflection;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace Gorecode.Bt.Spikes.GtSpike
{
    // Throwaway P1 helper: opens the spike graph in the GTK window for the
    // screenshot pass and reports the GTK module's window/importer API via
    // reflection. Delete after the spike.
    public static class SpikeWindowProbe
    {
        const string k_MainGraphPath = "Assets/Spikes/GtSpike/TestArtifacts/MainGraph.gtspike";

        [MenuItem("spikes/Probe GTK importers")]
        public static void ProbeImporters()
        {
            var asm = typeof(Graph).Assembly;
            var importerTypes = asm.GetTypes()
                .Where(t => typeof(UnityEditor.AssetImporters.ScriptedImporter).IsAssignableFrom(t) && !t.IsAbstract)
                .ToArray();

            foreach (var t in importerTypes)
            {
                var argSets = t.GetCustomAttributesData()
                    .Where(d => d.AttributeType == typeof(UnityEditor.AssetImporters.ScriptedImporterAttribute))
                    .Select(d => string.Join(",", d.ConstructorArguments.Select(a => System.Convert.ToString(a.Value))));
                Debug.Log("[GtSpike] importer " + t.FullName + " ctorArgs=[" + string.Join(" ; ", argSets) + "]");
            }

            AssetDatabase.ImportAsset(k_MainGraphPath, ImportAssetOptions.ForceUpdate);
            var main = AssetDatabase.LoadMainAssetAtPath(k_MainGraphPath);
            Debug.Log("[GtSpike] after ForceUpdate main asset = " + (main != null ? main.GetType().FullName : "null"));

            // List what GTK exposes for graph registration, to understand how
            // the extension -> graph type -> importer chain is supposed to bind.
            var graphAttrType = typeof(Graph);
            Debug.Log("[GtSpike] Graph attribute ctor params: "
                + string.Join(",", graphAttrType.GetCustomAttributesData().Select(d => d.AttributeType.Name)));
        }

        [MenuItem("spikes/Open GT Spike Graph (probe)")]
        public static void OpenAndProbe()
        {
            var graph = GraphDatabase.LoadGraph<SpikeGraph>(k_MainGraphPath);
            if (graph == null)
            {
                Debug.LogError("[GtSpike] MainGraph not found at " + k_MainGraphPath);
                return;
            }

            var asm = typeof(Graph).Assembly;
            var windowTypes = asm.GetTypes()
                .Where(t => typeof(EditorWindow).IsAssignableFrom(t) && !t.IsAbstract)
                .ToArray();

            foreach (var t in windowTypes)
            {
                var statics = t.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Where(m => m.GetParameters().Length > 0)
                    .Select(m => m.Name + "(" + string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name)) + ")")
                    .ToArray();
                Debug.Log("[GtSpike] window " + t.FullName + ": " + string.Join(" | ", statics));
            }

            foreach (var t in windowTypes)
            {
                foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    var ps = m.GetParameters();
                    if (ps.Length >= 1 && typeof(Graph).IsAssignableFrom(ps[0].ParameterType))
                    {
                        try
                        {
                            var args = new object[ps.Length];
                            args[0] = graph;
                            for (var i = 1; i < ps.Length; i++)
                            {
                                args[i] = ps[i].ParameterType.IsValueType
                                    ? Activator.CreateInstance(ps[i].ParameterType)
                                    : null;
                            }
                            m.Invoke(null, args);
                            Debug.Log("[GtSpike] invoked " + t.FullName + "." + m.Name + " - window should be open");
                            return;
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning("[GtSpike] " + t.FullName + "." + m.Name + " failed: "
                                + (e.InnerException != null ? e.InnerException.Message : e.Message));
                        }
                    }
                }
            }

            // The GTK window takes a GraphObject wrapper. The stack traces show
            // Unity.GraphToolkit.Editor.GraphObject is the public handle type.
            var asm2 = typeof(Graph).Assembly;
            var goType = asm2.GetType("Unity.GraphToolkit.Editor.GraphObject");
            if (goType != null)
            {
                var goFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
                var goMembers = goType.GetMethods(goFlags)
                    .Where(m => !m.IsSpecialName)
                    .Select(m => (m.IsStatic ? "static " : "") + m.Name + "("
                        + string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name)) + ")")
                    .ToArray();
                Debug.Log("[GtSpike] GraphObject members: " + string.Join(" | ", goMembers));

                // Look for anything shaped like an opener taking a path or guid.
                foreach (var m in goType.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    var ps = m.GetParameters();
                    var name = m.Name.ToLowerInvariant();
                    var looksLikeOpener = name.Contains("open") || name.Contains("show") || name.Contains("load");
                    var takesPathLike = ps.Length >= 1
                        && (ps[0].ParameterType == typeof(string) || ps[0].ParameterType.Name == "GUID");
                    if (looksLikeOpener && takesPathLike)
                    {
                        Debug.Log("[GtSpike] candidate opener: " + m.Name + "("
                            + string.Join(",", ps.Select(p => p.ParameterType.Name)) + ")");
                    }
                }

                // Load the wrapper and hand it to the GTK window. The (string)
                // overload is generic; use the (string, Type) overload.
                var loadMethod = goType.GetMethod("LoadGraphObjectAtPath", new[] { typeof(string), typeof(Type) });
                var graphObject = loadMethod != null
                    ? loadMethod.Invoke(null, new object[] { k_MainGraphPath, typeof(SpikeGraph) })
                    : null;
                Debug.Log("[GtSpike] LoadGraphObjectAtPath -> "
                    + (graphObject != null ? graphObject.GetType().FullName : "null"));

                var impWindowType2 = windowTypes.FirstOrDefault(t => t.Name == "GraphViewEditorWindowImp");
                var show = impWindowType2 != null
                    ? impWindowType2.GetMethod("ShowGraph", BindingFlags.Public | BindingFlags.Static)
                    : null;
                if (graphObject != null && show != null
                    && show.GetParameters()[0].ParameterType.IsInstanceOfType(graphObject))
                {
                    show.Invoke(null, new object[] { graphObject });
                    Debug.Log("[GtSpike] ShowGraph invoked - graph window should be open");
                    return;
                }
            }

            Debug.LogWarning("[GtSpike] probe complete; see GraphObject members above");
        }
    }
}
