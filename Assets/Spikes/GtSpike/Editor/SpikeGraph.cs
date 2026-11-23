using System;
using UnityEditor;
using Unity.GraphToolkit.Editor;

namespace Gorecode.Bt.Spikes.GtSpike
{
    // Throwaway Graph Toolkit viability spike (P1). Never referenced from product
    // assemblies (AGENTS.md asmdef rule).
    [Graph(AssetExtension, GraphOptions.SupportsSubgraphs)]
    [Serializable]
    public class SpikeGraph : Graph
    {
        public const string AssetExtension = "gtspike";

        [MenuItem("Assets/Create/spikes/GT Spike Graph", false)]
        static void CreateAssetFile()
        {
            GraphDatabase.PromptInProjectBrowserToCreateNewAsset<SpikeGraph>();
        }
    }
}
