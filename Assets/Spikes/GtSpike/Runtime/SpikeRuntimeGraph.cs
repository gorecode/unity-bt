using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gorecode.Bt.Spikes.GtSpike
{
    // Minimal compiled form of a SpikeGraph, produced by SpikeGraphImporter.
    // Throwaway P1 data model: node identity + type + layout only.
    public class SpikeRuntimeGraph : ScriptableObject
    {
        [Serializable]
        public class NodeRecord
        {
            public Hash128 Id;
            public string TypeName;
            public Vector2 Position;
        }

        public Hash128 GraphId;
        public List<NodeRecord> Nodes = new List<NodeRecord>();
    }
}
