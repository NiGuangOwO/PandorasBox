using ECommons.Automation;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Component.GUI;
using PandorasBox.FeaturesSetup;
using System;
using static ECommons.GenericHelpers;

namespace PandorasBox.Features.Other
{
    internal class ATMSolver : Feature
    {
        public override string Name { get; } = "自动动态演练 (俗称QTE)";
        public override string Description { get; } = "当动态演练在屏幕上时，自动为您点击按钮或捣碎。";

        public override FeatureType FeatureType => FeatureType.Other;
        private long Throttler { get; set; } = Environment.TickCount64;
        private Random random = new Random();
        public override void Enable()
        {
            Svc.Framework.Update += RunFeature;
            base.Enable();
        }

        bool hasDirectChat = false;

        private unsafe void RunFeature(IFramework framework)
        {
            if ((TryGetAddonByName<AtkUnitBase>("QTE", out var addon) && addon->IsVisible) || TryGetAddonByName<AtkUnitBase>("QTE", 2, out var addon2) && addon2->IsVisible)
            {
                DisableDirectChatIfNeeded();

                if (Environment.TickCount64 >= Throttler)
                {
                    if (ChatLogIsFocused())
                        WindowsKeypress.SendKeypress(ECommons.Interop.LimitedKeys.Escape);

                    WindowsKeypress.SendKeypress(ECommons.Interop.LimitedKeys.X); //Mashes to try and resolve the QTE
                    Throttler = Environment.TickCount64 + random.Next(25, 50);
                }
            }
            else
            {
                EnableDirectChatIfNeeded();
            }
        }

        private unsafe bool ChatLogIsFocused()
        {
            var stage = AtkStage.Instance();
            var unitManagers = &stage->RaptureAtkUnitManager->AtkUnitManager.FocusedUnitsList;

            foreach (var i in unitManagers->Entries)
            {
                if (i.Value != null)
                {
                    var addonName = i.Value->NameString;
                    if (addonName == "ChatLog")
                        return true;
                }
            }

            return false;
        }

        private void EnableDirectChatIfNeeded()
        {
            if (hasDirectChat)
            {
                Svc.GameConfig.UiControl.Set("DirectChat", true);
                hasDirectChat = false;
            }
        }

        private void DisableDirectChatIfNeeded()
        {
            if (Svc.GameConfig.UiControl.GetBool("DirectChat"))
            {
                Svc.GameConfig.UiControl.Set("DirectChat", false);
                hasDirectChat = true;
            }
        }

        public override void Disable()
        {
            Svc.Framework.Update -= RunFeature;
            base.Disable();
        }
    }
}
