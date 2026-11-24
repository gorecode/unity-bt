using UnityEngine;

namespace Gorecode.Bt.Spikes.GtSpike
{
    // Look shared by the spike nodes.
    public static class SpikeNodeLook
    {
        // Passed through [Node(.., stylesheet)]: Graph Toolkit draws every node card
        // 124 px wide, which truncates most titles (docs/gt-notes.md).
        public const string StylesheetPath = "Assets/Spikes/GtSpike/Editor/SpikeNode.uss";

        // Accent colors by node category (the strip on a node's top border).
        public static readonly Color Composite = new Color(0.30f, 0.58f, 0.86f);
        public static readonly Color Task = new Color(0.24f, 0.71f, 0.55f);
        public static readonly Color Guard = new Color(0.64f, 0.45f, 0.88f);
    }
}
