using ECommons.Automation;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using PandorasBox.FeaturesSetup;
using PandorasBox.Helpers;
using PandorasBox.UI;
using System;
using System.Numerics;

namespace PandorasBox.Features.UI
{
    public unsafe class TradeAllCollectibles : Feature
    {
        public override string Name => "交易所有收藏品";

        public override string Description => "将收藏品界面上的“交易”按钮替换为所选收藏品的“全部交易”按钮。";

        public override FeatureType FeatureType => FeatureType.UI;

        public bool Trading { get; private set; } = false;

        internal Overlays overlay;
        public override void Enable()
        {
            overlay = new(this);
            base.Enable();
        }

        public override bool DrawConditions()
        {
            return Svc.GameGui.GetAddonByName("CollectablesShop") != IntPtr.Zero;
        }

        public override void Draw()
        {
            if (Svc.GameGui.GetAddonByName("CollectablesShop") != IntPtr.Zero)
            {
                var addon = (AtkUnitBase*)Svc.GameGui.GetAddonByName("CollectablesShop");
                if (!addon->IsVisible) return;

                var tradeButton = addon->UldManager.NodeList[2];

                if (tradeButton->IsVisible())
                    tradeButton->ToggleVisibility(false);

                var position = AtkResNodeHelper.GetNodePosition(tradeButton);
                var scale = AtkResNodeHelper.GetNodeScale(tradeButton);
                var size = new Vector2(tradeButton->Width, tradeButton->Height) * scale;

                ImGuiHelpers.ForceNextWindowMainViewport();
                ImGuiHelpers.SetNextWindowPosRelativeMainViewport(position);

                ImGui.PushStyleColor(ImGuiCol.WindowBg, 0);
                var oldSize = ImGui.GetFont().Scale;
                ImGui.GetFont().Scale *= scale.X;
                ImGui.PushFont(ImGui.GetFont());
                ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 0f.Scale());
                ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(0f.Scale(), 0f.Scale()));
                ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0f.Scale(), 0f.Scale()));
                ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f.Scale());
                ImGui.PushStyleVar(ImGuiStyleVar.WindowMinSize, size);
                ImGui.Begin($"###RepairAll{tradeButton->NodeId}", ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoNavFocus
                    | ImGuiWindowFlags.AlwaysUseWindowPadding | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoSavedSettings);


                if (!Trading)
                {
                    if (ImGui.Button($"交易全部###StartTrade", size))
                    {
                        Trading = true;
                        TryTradeAll();
                    }
                }
                else
                {
                    if (ImGui.Button($"交易中...点击来中止###AbortTrade", size))
                    {
                        Trading = false;
                        TaskManager.Abort();
                    }
                }

                ImGui.End();
                ImGui.PopStyleVar(5);
                ImGui.GetFont().Scale = oldSize;
                ImGui.PopFont();
                ImGui.PopStyleColor();
            }
        }

        private void TryTradeAll()
        {
            var addon = (AtkUnitBase*)Svc.GameGui.GetAddonByName("CollectablesShop");
            if (!addon->IsVisible) return;

            var list = addon->UldManager.NodeList[22]->GetAsAtkComponentList();
            var listCount = list->ListLength;

            if (listCount == 0)
            {
                Trading = false;
                TaskManager.Abort();
                return;
            }

            for (var i = 1; i <= listCount; i++)
            {
                TaskManager.Enqueue(() =>
                {
                    if (Svc.GameGui.GetAddonByName("SelectYesno") != IntPtr.Zero)
                    {
                        Trading = false;
                        TaskManager.Abort();
                    }
                });
                TaskManager.Enqueue(() => Callback.Fire(addon, false, 15, (uint)0), $"Trading{i}");
                TaskManager.EnqueueDelay(500);
            }
            TaskManager.Enqueue(() => { Trading = false; TaskManager.Abort(); });
        }

        public override void Disable()
        {
            P.Ws.RemoveWindow(overlay);
            overlay = null!;
            ReEnableButton();
            base.Disable();
        }

        private void ReEnableButton()
        {
            if (Svc.GameGui.GetAddonByName("CollectablesShop") != IntPtr.Zero)
            {
                var addon = (AtkUnitBase*)Svc.GameGui.GetAddonByName("CollectablesShop");
                if (!addon->IsVisible) return;

                var tradeButton = addon->UldManager.NodeList[2];

                if (!tradeButton->IsVisible())
                    tradeButton->ToggleVisibility(true);


            }
        }
    }
}
