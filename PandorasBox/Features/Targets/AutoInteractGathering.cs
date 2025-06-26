using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text.SeStringHandling;
using ECommons;
using ECommons.Automation;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.MJI;
using Lumina.Excel.Sheets;
using PandorasBox.FeaturesSetup;
using PandorasBox.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace PandorasBox.Features.Targets
{
    public unsafe class AutoInteractGathering : Feature
    {

        public override string Name => "自动与采集点交互";
        public override string Description => "当距离足够近并且当前职业正确时与树木和岩石交互。";

        public override FeatureType FeatureType => FeatureType.Targeting;

        public override bool UseAutoConfig => true;

        public class Configs : FeatureConfig
        {
            [FeatureConfigOption("设置延迟 (秒)", FloatMin = 0.1f, FloatMax = 10f, EditorSize = 300, EnforcedLimit = true)]
            public float Throttle = 0.1f;

            [FeatureConfigOption("采集后冷却 (秒)", FloatMin = 0.1f, FloatMax = 10f, EditorSize = 300, EnforcedLimit = true)]
            public float Cooldown = 0.1f;

            [FeatureConfigOption("排除未知的采集地点", "", 1)]
            public bool ExcludeTimedUnspoiled = false;

            [FeatureConfigOption("排除限时的采集地点", "", 2)]
            public bool ExcludeTimedEphermeral = false;

            [FeatureConfigOption("排除传说的采集地点", "", 3)]
            public bool ExcludeTimedLegendary = false;

            [FeatureConfigOption("交互需要的GP (>=)", IntMin = 0, IntMax = 1000, EditorSize = 300)]
            public int RequiredGP = 0;

            [FeatureConfigOption("排除空岛的采集地点", "", 7)]
            public bool ExcludeIsland = false;

            [FeatureConfigOption("排除采矿工的采集地点", "", 4)]
            public bool ExcludeMiner = false;

            [FeatureConfigOption("排除园艺工的采集地点", "", 5)]
            public bool ExcludeBotanist = false;

            [FeatureConfigOption("排除刺鱼的采集地点", "", 6)]
            public bool ExcludeFishing = false;

            [FeatureConfigOption("Zone Whitelist", "TerritorySelection", 7)]
            public List<uint> ZoneWhitelist = [];
        }

        public Configs Config { get; private set; }

        public override void Enable()
        {
            Config = LoadConfig<Configs>() ?? new Configs();
            Svc.Framework.Update += RunFeature;
            Svc.Condition.ConditionChange += TriggerCooldown;
            Svc.Toasts.ErrorToast += CheckIfLanding;
            base.Enable();
        }

        private void CheckIfLanding(ref SeString message, ref bool isHandled)
        {
            if (message.GetText() == Svc.Data.GetExcelSheet<LogMessage>().First(x => x.RowId == 7777).Text.ExtractText())
            {
                TaskManager.Abort();
                TaskManager.EnqueueDelay(2000);
            }
        }

        private void TriggerCooldown(ConditionFlag flag, bool value)
        {
            if (flag == ConditionFlag.Gathering && !value)
                TaskManager.EnqueueDelay((int)(Config.Cooldown * 1000));
        }

        private void RunFeature(IFramework framework)
        {
            if (Svc.Condition[ConditionFlag.Gathering] || Svc.Condition[ConditionFlag.OccupiedInQuestEvent])
                return;

            if (Svc.ClientState.LocalPlayer is null) return;
            if (Svc.ClientState.LocalPlayer.IsCasting) return;
            if (Svc.Condition[ConditionFlag.Jumping]) return;
            if (Config.ZoneWhitelist.Count > 0 && !Config.ZoneWhitelist.Contains(Player.Territory)) return;

            var nearbyNodes = Svc.Objects.Where(x => (x.ObjectKind == Dalamud.Game.ClientState.Objects.Enums.ObjectKind.GatheringPoint || x.ObjectKind == Dalamud.Game.ClientState.Objects.Enums.ObjectKind.CardStand) && Vector3.Distance(x.Position, Player.Object.Position) < 4 && GameObjectHelper.GetHeightDifference(x) <= 4 && x.IsTargetable).ToList();
            if (nearbyNodes.Count == 0)
                return;

            var nearestNode = nearbyNodes.OrderBy(GameObjectHelper.GetTargetDistance).First();
            var baseObj = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)nearestNode.Address;

            if (!nearestNode.IsTargetable)
                return;

            if (Config.ExcludeIsland && MJIManager.Instance()->IsPlayerInSanctuary != 0)
            {
                return;
            }

            if (nearestNode.ObjectKind == Dalamud.Game.ClientState.Objects.Enums.ObjectKind.CardStand && MJIManager.Instance()->IsPlayerInSanctuary != 0 && MJIManager.Instance()->CurrentMode == 1)
            {
                if (!TaskManager.IsBusy)
                {
                    TaskManager.EnqueueDelay((int)(Config.Throttle * 1000));
                    TaskManager.EnqueueWithTimeout(() => { TargetSystem.Instance()->OpenObjectInteraction(baseObj); return true; }, 1000);
                }
                return;
            }

            var gatheringPoint = Svc.Data.GetExcelSheet<GatheringPoint>().First(x => x.RowId == nearestNode.DataId);
            var job = gatheringPoint.GatheringPointBase.Value.GatheringType.Value.RowId;
            var targetGp = Math.Min(Config.RequiredGP, Svc.ClientState.LocalPlayer.MaxGp);

            string Folklore = "";

            if (gatheringPoint.GatheringSubCategory.IsValid && !gatheringPoint.GatheringSubCategory.Value.FolkloreBook.IsEmpty)
                Folklore = gatheringPoint.GatheringSubCategory.Value.FolkloreBook.ToString();

            if (Svc.Data.GetExcelSheet<GatheringPointTransient>().Any(x => x.RowId == nearestNode.DataId && x.GatheringRarePopTimeTable.Value.RowId > 0 && gatheringPoint.GatheringSubCategory.Value.Item.RowId == 0) && Config.ExcludeTimedUnspoiled)
                return;
            if (Svc.Data.GetExcelSheet<GatheringPointTransient>().Any(x => x.RowId == nearestNode.DataId && x.EphemeralStartTime != 65535) && Config.ExcludeTimedEphermeral)
                return;
            if (Svc.Data.GetExcelSheet<GatheringPointTransient>().Any(x => x.RowId == nearestNode.DataId && x.GatheringRarePopTimeTable.Value.RowId > 0 && Folklore.Length > 0 && gatheringPoint.GatheringSubCategory.Value.Item.RowId != 0) && Config.ExcludeTimedLegendary)
                return;

            if (!Config.ExcludeMiner && job is 0 or 1 && Svc.ClientState.LocalPlayer.ClassJob.RowId == 16 && Svc.ClientState.LocalPlayer.CurrentGp >= targetGp && !TaskManager.IsBusy)
            {
                TaskManager.EnqueueDelay((int)(Config.Throttle * 1000));
                TaskManager.Enqueue(() => { Chat.Instance.SendMessage("/automove off"); });
                TaskManager.EnqueueWithTimeout(() => { TargetSystem.Instance()->OpenObjectInteraction(baseObj); return true; }, 1000);
                return;
            }
            if (!Config.ExcludeBotanist && job is 2 or 3 && Svc.ClientState.LocalPlayer.ClassJob.RowId == 17 && Svc.ClientState.LocalPlayer.CurrentGp >= targetGp && !TaskManager.IsBusy)
            {
                TaskManager.EnqueueDelay((int)(Config.Throttle * 1000));
                TaskManager.Enqueue(() => { Chat.Instance.SendMessage("/automove off"); });
                TaskManager.EnqueueWithTimeout(() => { TargetSystem.Instance()->OpenObjectInteraction(baseObj); return true; }, 1000);
                return;
            }
            if (!Config.ExcludeFishing && job is 4 or 5 && Svc.ClientState.LocalPlayer.ClassJob.RowId == 18 && Svc.ClientState.LocalPlayer.CurrentGp >= targetGp && !TaskManager.IsBusy)
            {
                TaskManager.EnqueueDelay((int)(Config.Throttle * 1000));
                TaskManager.Enqueue(() => { Chat.Instance.SendMessage("/automove off"); });
                TaskManager.EnqueueWithTimeout(() => { TargetSystem.Instance()->OpenObjectInteraction(baseObj); return true; }, 1000);
                return;
            }

        }

        public override void Disable()
        {
            SaveConfig(Config);
            Svc.Framework.Update -= RunFeature;
            base.Disable();
        }
    }
}
