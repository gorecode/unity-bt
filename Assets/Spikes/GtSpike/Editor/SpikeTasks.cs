using System;
using Unity.GraphToolkit.Editor;

namespace Gorecode.Bt.Spikes.GtSpike
{
    // Task-shaped spike nodes exercising node options (custom inspector fields):
    // float/int/string values, Delayed() where rebuilding ports on each keystroke
    // would matter.
    [Serializable]
    [Node("Spike/Tasks")]
    public class SpikeMoveTo : Node
    {
        const string k_SpeedName = "Speed";
        const string k_StoppingDistanceName = "StoppingDistance";

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            context.AddOption<float>(k_SpeedName)
                .WithDisplayName("Speed")
                .WithDefaultValue(3f)
                .Delayed();
            context.AddOption<float>(k_StoppingDistanceName)
                .WithDisplayName("Stopping Distance")
                .WithDefaultValue(0.2f)
                .Delayed();
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort("In").WithCapacity(PortCapacity.Single).Build();
            context.AddOutputPort("Out").WithCapacity(PortCapacity.Single).Build();
        }
    }

    [Serializable]
    [Node("Spike/Tasks")]
    public class SpikeWait : Node
    {
        const string k_SecondsName = "Seconds";

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            context.AddOption<float>(k_SecondsName)
                .WithDisplayName("Seconds")
                .WithDefaultValue(1f)
                .Delayed();
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort("In").WithCapacity(PortCapacity.Single).Build();
            context.AddOutputPort("Out").WithCapacity(PortCapacity.Single).Build();
        }
    }

    [Serializable]
    [Node("Spike/Tasks")]
    public class SpikeLog : Node
    {
        const string k_MessageName = "Message";

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            context.AddOption<string>(k_MessageName).WithDisplayName("Message");
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort("In").WithCapacity(PortCapacity.Single).Build();
            context.AddOutputPort("Out").WithCapacity(PortCapacity.Single).Build();
        }
    }

    [Serializable]
    [Node("Spike/Tasks")]
    public class SpikeBlackboardWrite : Node
    {
        const string k_KeyNameName = "KeyName";
        const string k_ValueName = "Value";

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            context.AddOption<string>(k_KeyNameName).WithDisplayName("Key Name");
            context.AddOption<float>(k_ValueName)
                .WithDisplayName("Value")
                .WithDefaultValue(0f)
                .Delayed();
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort("In").WithCapacity(PortCapacity.Single).Build();
            context.AddOutputPort("Out").WithCapacity(PortCapacity.Single).Build();
        }
    }
}
