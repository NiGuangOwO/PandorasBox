using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Component.GUI;
using PandorasBox.FeaturesSetup;
using System;

namespace PandorasBox.Features.UI
{
    public unsafe class AutoFocus : Feature
    {
        public override string Name => "自动聚焦市场版搜索";

        public override string Description => "自动聚焦到市场板的搜索框上";

        public override FeatureType FeatureType => FeatureType.UI;

        public override void Enable()
        {
            Svc.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "ItemSearch", AddonSetup);
            base.Enable();
        }

        private void AddonSetup(AddonEvent type, AddonArgs args)
        {
            var addon = (AtkUnitBase*)args.Addon;
            if (args.AddonName != "ItemSearch" || addon == null) return;
            if (addon->CollisionNodeList == null) addon->UpdateCollisionNodeList(false);
            if (addon->CollisionNodeList == null) return;
            addon->SetFocusNode(addon->CollisionNodeList[11]);
        }

        public override void Disable()
        {
            Svc.AddonLifecycle.UnregisterListener(AddonSetup);
            base.Disable();
        }
    }
}
