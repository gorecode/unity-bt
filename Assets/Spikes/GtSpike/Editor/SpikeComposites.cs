using System;
using Unity.GraphToolkit.Editor;

namespace Gorecode.Bt.Spikes.GtSpike
{
    // Composite-shaped spike nodes. Flow ports are untyped; Single capacity pins
    // keep the toy topology close to a behavior tree.
    [Serializable]
    [Node("Spike/Composites")]
    public class SpikeRoot : Node
    {
        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddOutputPort("Out").WithCapacity(PortCapacity.Single).Build();
        }
    }

    [Serializable]
    [Node("Spike/Composites")]
    public class SpikeSelector : Node
    {
        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort("In").WithCapacity(PortCapacity.Single).Build();
            context.AddOutputPort("Children").Build();
        }
    }

    [Serializable]
    [Node("Spike/Composites")]
    public class SpikeSequence : Node
    {
        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort("In").WithCapacity(PortCapacity.Single).Build();
            context.AddOutputPort("Children").Build();
        }
    }

    [Serializable]
    [Node("Spike/Composites")]
    public class SpikeParallel : Node
    {
        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort("In").WithCapacity(PortCapacity.Single).Build();
            context.AddOutputPort("Main").WithCapacity(PortCapacity.Single).Build();
            context.AddOutputPort("Background").WithCapacity(PortCapacity.Single).Build();
        }
    }
}
