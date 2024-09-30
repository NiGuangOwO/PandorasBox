using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Memory;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;
using PandorasBox.FeaturesSetup;
using System;
using System.Linq;

namespace PandorasBox.Features.UI
{
    public unsafe class QuickLowerQuality : Feature
    {
        public override string Name => "快速降低品质";

        public override string Description => "自动确认降低品质的弹出菜单。";

        public override FeatureType FeatureType => FeatureType.UI;

        public override void Enable()
        {
            Svc.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "SelectYesno", PostSetup);
            base.Enable();
        }

        private void PostSetup(AddonEvent type, AddonArgs args)
        {
            var addon = (AtkUnitBase*)args.Addon;
            var seString = MemoryHelper.ReadSeStringNullTerminated(new IntPtr(addon->AtkValues[0].String));
            if (seString.Payloads.Count < 3 || seString.Payloads[2] is not TextPayload payload2)
            {
                return;
            }
            var rawText = payload2.Text!.Trim();
            var trimmedText = rawText.Remove(rawText.LastIndexOf(' ')).TrimEnd();
            var sheetText = Svc.Data.GetExcelSheet<Addon>()!.First(x => x.RowId == 155).Text.Payloads[2].RawString.Trim();

            if (sheetText == trimmedText)
            {
                var values = stackalloc AtkValue[5];
                values[0] = new AtkValue
                {
                    Type = FFXIVClientStructs.FFXIV.Component.GUI.ValueType.Int,
                    Int = 0
                };
                addon->FireCallback(1, values, true);
            }
        }

        public override void Disable()
        {
            Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, "SelectYesno", PostSetup);
            base.Disable();
        }
    }
}
