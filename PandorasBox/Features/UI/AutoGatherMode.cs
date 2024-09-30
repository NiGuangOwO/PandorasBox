using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using ECommons.Automation;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Component.GUI;
using PandorasBox.FeaturesSetup;

namespace PandorasBox.Features.UI
{
    public unsafe class AutoGatherMode : Feature
    {
        public override string Name => "[国服限定] 无人岛自动切换收获模式";

        public override string Description => "进入无人岛后自动切换到收获模式。";

        public override FeatureType FeatureType => FeatureType.UI;


        public override void Enable()
        {
            Svc.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "MJIHud", SwitchGatherMode);
            base.Enable();
        }

        private void SwitchGatherMode(AddonEvent eventType, AddonArgs addonInfo)
        {
            TaskManager.Enqueue(() => Callback.Fire((AtkUnitBase*)addonInfo.Addon, true, 11, 0));
            TaskManager.Enqueue(() => Callback.Fire((AtkUnitBase*)Svc.GameGui.GetAddonByName("ContextIconMenu"), true, 0, 1, 82042, 0, 0));
        }

        public override void Disable()
        {
            Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, "MJIHud", SwitchGatherMode);
            base.Disable();
        }
    }
}
