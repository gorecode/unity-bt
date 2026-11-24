using System;
using Unity.GraphToolkit.Editor;

namespace Gorecode.Bt.Spikes.GtSpike
{
    // Composite-shaped spike nodes. Ports are vertical — entry on top, exits at the
    // bottom — so the tree reads top-down, away from the root.
    [Serializable]
    [Node("Spike/Composites", null, "Root", SpikeNodeLook.StylesheetPath)]
    public class SpikeRoot : Node
    {
        public override void OnEnable()
        {
            base.OnEnable();
            DefaultColor = SpikeNodeLook.Composite;
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddOutputPort("Out").WithCapacity(PortCapacity.Single).AsVertical().Build();
        }
    }

    [Serializable]
    [Node("Spike/Composites", null, "Selector", SpikeNodeLook.StylesheetPath)]
    public class SpikeSelector : Node
    {
        public override void OnEnable()
        {
            base.OnEnable();
            DefaultColor = SpikeNodeLook.Composite;
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort("In").WithCapacity(PortCapacity.Single).AsVertical().Build();
            context.AddOutputPort("Children").AsVertical().Build();
        }
    }

    [Serializable]
    [Node("Spike/Composites", null, "Sequence", SpikeNodeLook.StylesheetPath)]
    public class SpikeSequence : Node
    {
        public override void OnEnable()
        {
            base.OnEnable();
            DefaultColor = SpikeNodeLook.Composite;
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort("In").WithCapacity(PortCapacity.Single).AsVertical().Build();
            context.AddOutputPort("Children").AsVertical().Build();
        }
    }

    [Serializable]
    [Node("Spike/Composites", null, "Parallel", SpikeNodeLook.StylesheetPath)]
    public class SpikeParallel : Node
    {
        public override void OnEnable()
        {
            base.OnEnable();
            DefaultColor = SpikeNodeLook.Composite;
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort("In").WithCapacity(PortCapacity.Single).AsVertical().Build();
            context.AddOutputPort("Main").WithCapacity(PortCapacity.Single).AsVertical().Build();
            context.AddOutputPort("Background").WithCapacity(PortCapacity.Single).AsVertical().Build();
        }
    }
}
