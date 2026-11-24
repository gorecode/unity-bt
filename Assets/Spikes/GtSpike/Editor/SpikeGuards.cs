using System;
using Unity.GraphToolkit.Editor;

namespace Gorecode.Bt.Spikes.GtSpike
{
    // Guard-shaped spike nodes. SpikeBlackboardCondition mirrors the spec's
    // S54 vocabulary (KeyQuery x NotifyMode) to sanity-check enum options.
    // Entry on top, exit at the bottom.
    [Serializable]
    [Node("Spike/Guards", null, "Key Condition", SpikeNodeLook.StylesheetPath)]
    public class SpikeBlackboardCondition : Node
    {
        public enum KeyQuery { IsSet, IsNotSet }
        public enum NotifyMode { OnResultChange, OnValueChange }

        const string k_KeyQueryName = "KeyQuery";
        const string k_NotifyModeName = "NotifyMode";

        public override void OnEnable()
        {
            base.OnEnable();
            DefaultColor = SpikeNodeLook.Guard;
        }

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            context.AddOption<KeyQuery>(k_KeyQueryName).WithDisplayName("Key Query");
            context.AddOption<NotifyMode>(k_NotifyModeName).WithDisplayName("Notify Mode");
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort("In").WithCapacity(PortCapacity.Single).AsVertical().Build();
            context.AddOutputPort("Out").WithCapacity(PortCapacity.Single).AsVertical().Build();
        }
    }

    [Serializable]
    [Node("Spike/Guards", null, "Cooldown", SpikeNodeLook.StylesheetPath)]
    public class SpikeCooldown : Node
    {
        const string k_SecondsName = "Seconds";

        public override void OnEnable()
        {
            base.OnEnable();
            DefaultColor = SpikeNodeLook.Guard;
        }

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            context.AddOption<float>(k_SecondsName)
                .WithDisplayName("Seconds")
                .WithDefaultValue(2f)
                .Delayed();
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort("In").WithCapacity(PortCapacity.Single).AsVertical().Build();
            context.AddOutputPort("Out").WithCapacity(PortCapacity.Single).AsVertical().Build();
        }
    }

    [Serializable]
    [Node("Spike/Guards", null, "Loop", SpikeNodeLook.StylesheetPath)]
    public class SpikeLoop : Node
    {
        public enum LoopMode { Finite, Infinite }

        const string k_ModeName = "Mode";
        const string k_CountName = "Count";
        const string k_TimeoutName = "Timeout";

        public override void OnEnable()
        {
            base.OnEnable();
            DefaultColor = SpikeNodeLook.Guard;
        }

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            context.AddOption<LoopMode>(k_ModeName).WithDisplayName("Mode");
            context.AddOption<int>(k_CountName)
                .WithDisplayName("Count")
                .WithDefaultValue(3)
                .Delayed();
            context.AddOption<float>(k_TimeoutName)
                .WithDisplayName("Timeout (s)")
                .WithDefaultValue(-1f)
                .Delayed();
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort("In").WithCapacity(PortCapacity.Single).AsVertical().Build();
            context.AddOutputPort("Out").WithCapacity(PortCapacity.Single).AsVertical().Build();
        }
    }
}
