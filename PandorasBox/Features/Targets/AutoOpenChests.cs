using Dalamud.Game.ClientState.Objects.Enums;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;
using PandorasBox.FeaturesSetup;
using System;
using System.Linq;
using System.Numerics;

namespace PandorasBox.Features.Targets
{
    public unsafe class AutoOpenChests : Feature
    {
        public override string Name => "自动打开宝箱";

        public override string Description => "靠近宝箱时自动打开。";

        public override FeatureType FeatureType => FeatureType.Targeting;

        public class Configs : FeatureConfig
        {
            [FeatureConfigOption("打开后立刻自动关闭战利品窗口", "", 1)]
            public bool CloseLootWindow = false;

            [FeatureConfigOption("在高难度任务中打开宝箱", "", 2)]
            public bool OpenInHighEndDuty = false;
        }

        public Configs Config { get; private set; }

        public override bool UseAutoConfig => true;

        public override void Enable()
        {
            Config = LoadConfig<Configs>() ?? new Configs();
            Svc.Framework.Update += RunFeature;
            base.Enable();
        }

        private static DateTime NextOpenTime = DateTime.Now;
        private static ulong LastChestId = 0;

        private void RunFeature(IFramework framework)
        {
            CloseWindow();

            if (Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas])
                return;

            var contentFinderInfo = Svc.Data.GetExcelSheet<ContentFinderCondition>()!.GetRow(GameMain.Instance()->CurrentContentFinderConditionId);
            if (!Config.OpenInHighEndDuty && contentFinderInfo is not null && contentFinderInfo.HighEndDuty)
                return;

            var player = Player.Object;
            if (player == null) return; 
            var treasure = Svc.Objects.FirstOrDefault(o =>
            {
                if (o == null) return false;
                var dis = Vector3.Distance(player.Position, o.Position) - player.HitboxRadius - o.HitboxRadius;
                if (dis > 0.5f) return false;

                var obj = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)(void*)o.Address;
                if (!obj->GetIsTargetable()) return false;
                if ((ObjectKind)obj->ObjectKind != ObjectKind.Treasure) return false;

                // Opened
                foreach (var item in Loot.Instance()->Items)
                    if (item.ChestObjectId == o.GameObjectId) return false;

                return true;
            });

            if (treasure == null) return;
            if (DateTime.Now < NextOpenTime) return;
            if (treasure.GameObjectId == LastChestId && DateTime.Now - NextOpenTime < TimeSpan.FromSeconds(10)) return;

            NextOpenTime = DateTime.Now.AddSeconds(new Random().NextDouble() + 0.2);
            LastChestId = treasure.GameObjectId;

            try
            {
                Svc.Targets.Target = treasure;
                TargetSystem.Instance()->InteractWithObject((FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)(void*)treasure.Address);
                if (Config.CloseLootWindow)
                {
                    CloseWindowTime = DateTime.Now.AddSeconds(0.5);
                }
            }
            catch (Exception ex)
            {
                Svc.Log.Error(ex, "Failed to open the chest!");
            }
        }

        private static DateTime CloseWindowTime = DateTime.Now;
        private static unsafe void CloseWindow()
        {
            if (CloseWindowTime < DateTime.Now) return;
            if (Svc.GameGui.GetAddonByName("NeedGreed", 1) != IntPtr.Zero)
            {
                var needGreedWindow = (AtkUnitBase*)Svc.GameGui.GetAddonByName("NeedGreed", 1);
                if (needGreedWindow == null) return;

                if (needGreedWindow->IsVisible)
                {
                    needGreedWindow->Close(true);
                    return;
                }
            }
            else
            {
                return;
            }

            return;
        }

        public override void Disable()
        {
            SaveConfig(Config);
            Svc.Framework.Update -= RunFeature;
            base.Disable();
        }
    }
}
