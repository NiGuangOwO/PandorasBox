using ECommons.Automation;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Lumina.Excel.GeneratedSheets;
using PandorasBox.FeaturesSetup;
using PandorasBox.Helpers;
using System.Linq;

namespace PandorasBox.Features.Other
{
    public unsafe class AutoSwitchGatherer : Feature
    {
        public override string Name => "自动切换采集职业";

        public override string Description => "当接近采集点时，并且“三角测量”和“矿脉勘探”都处于激活状态时，自动切换到适合的采集职业。职业必须有一个套装预设才能切换到。";

        public override FeatureType FeatureType => FeatureType.Other;

        public class Configs : FeatureConfig
        {
            [FeatureConfigOption("设置延迟 (秒)", FloatMin = 0.1f, FloatMax = 10f, EditorSize = 300, EnforcedLimit = true)]
            public float Throttle = 0.1f;
        }

        public Configs Config { get; private set; }

        public override bool UseAutoConfig => true;

        public override void Enable()
        {
            Config = LoadConfig<Configs>() ?? new Configs();
            Svc.Framework.Update += RunFeature;
            base.Enable();
        }

        private void RunFeature(IFramework framework)
        {
            if (Svc.ClientState.LocalPlayer == null) return;

            var nearbyNodes = Svc.Objects.Where(x => x.ObjectKind == Dalamud.Game.ClientState.Objects.Enums.ObjectKind.GatheringPoint && GameObjectHelper.GetTargetDistance(x) <= 10).ToList();
            if (nearbyNodes.Count == 0)
                return;

            var nearestNode = nearbyNodes.OrderBy(GameObjectHelper.GetTargetDistance).First();
            var baseObj = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)nearestNode.Address;

            if (!baseObj->GetIsTargetable())
                return;

            var gatheringPoint = Svc.Data.GetExcelSheet<GatheringPoint>().First(x => x.RowId == nearestNode.DataId);
            var job = gatheringPoint.GatheringPointBase.Value.GatheringType.Value.RowId;

            if (Svc.ClientState.LocalPlayer.StatusList.Where(x => x.StatusId == 217 || x.StatusId == 225).Count() != 2 && (job is 0 or 1 or 2 or 3))
                return;

            if (job is 0 or 1 && Svc.ClientState.LocalPlayer.ClassJob.Id != 16 && !TaskManager.IsBusy)
            {
                TaskManager.DelayNext("AutoSwitchGather", (int)(Config.Throttle * 1000));
                TaskManager.Enqueue(() => SwitchJobGearset(16));
            }
            if (job is 2 or 3 && Svc.ClientState.LocalPlayer.ClassJob.Id != 17 && !TaskManager.IsBusy)
            {
                TaskManager.DelayNext("AutoSwitchGather", (int)(Config.Throttle * 1000));
                TaskManager.Enqueue(() => SwitchJobGearset(17));
            }
            if (job is 4 or 5 && Svc.ClientState.LocalPlayer.ClassJob.Id != 18 && !TaskManager.IsBusy)
            {
                TaskManager.DelayNext("AutoSwitchGather", (int)(Config.Throttle * 1000));
                TaskManager.Enqueue(() => SwitchJobGearset(18));
            }
        }

        private static unsafe bool SwitchJobGearset(uint cjID)
        {
            if (Svc.ClientState.LocalPlayer.ClassJob.Id == cjID) return true;
            var gs = GetGearsetForClassJob(cjID);
            if (gs is null) return true;

            Chat chat = new();
            chat.SendMessage($"/gearset change {gs.Value + 1}");

            return true;
        }

        private static unsafe byte? GetGearsetForClassJob(uint cjId)
        {
            var gearsetModule = RaptureGearsetModule.Instance();
            for (var i = 0; i < 100; i++)
            {
                var gearset = gearsetModule->GetGearset(i);
                if (gearset == null) continue;
                if (!gearset->Flags.HasFlag(RaptureGearsetModule.GearsetFlag.Exists)) continue;
                if (gearset->Id != i) continue;
                if (gearset->ClassJob == cjId) return gearset->Id;
            }
            return null;
        }

        public override void Disable()
        {
            SaveConfig(Config);
            Svc.Framework.Update -= RunFeature;
            base.Disable();
        }
    }
}
