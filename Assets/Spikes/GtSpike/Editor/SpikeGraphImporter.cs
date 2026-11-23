using System.Linq;
using Unity.GraphToolkit.Editor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Gorecode.Bt.Spikes.GtSpike
{
    // Documented GTK pattern: a ScriptedImporter registered on the graph's
    // extension compiles the authored graph into a plain runtime asset. The
    // .gtspike file behaves as a SpikeRuntimeGraph in inspectors afterwards.
    [ScriptedImporter(1, SpikeGraph.AssetExtension)]
    public class SpikeGraphImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var graph = GraphDatabase.LoadGraphForImporter<SpikeGraph>(ctx.assetPath);
            if (graph == null)
            {
                Debug.LogError("[GtSpike] Failed to load graph: " + ctx.assetPath);
                return;
            }

            var runtime = ScriptableObject.CreateInstance<SpikeRuntimeGraph>();
            runtime.GraphId = graph.ID;

            foreach (var node in graph.GetNodes())
            {
                runtime.Nodes.Add(new SpikeRuntimeGraph.NodeRecord
                {
                    Id = node.ID,
                    TypeName = node.GetType().Name,
                    Position = node.Position,
                });
            }

            ctx.AddObjectToAsset("Runtime", runtime);
            ctx.SetMainObject(runtime);
        }
    }
}
