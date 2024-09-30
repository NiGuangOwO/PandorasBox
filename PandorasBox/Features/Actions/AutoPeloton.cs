using Dalamud.Game.ClientState.Conditions;
using ECommons.DalamudServices;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using PandorasBox.FeaturesSetup;
using PandorasBox.Helpers;
using System.Linq;
using System.Text.RegularExpressions;

namespace PandorasBox.Features
{
    public unsafe class AutoPeloton : Feature
    {
        public override string Name => "自动速行";

        public override string Description => "在战斗外自动使用速行。（仅远敏）";

        public override FeatureType FeatureType => FeatureType.Actions;

        public override bool UseAutoConfig => false;

        public class Configs : FeatureConfig
        {
            [FeatureConfigOption("设置延迟（秒）", FloatMin = 0.1f, FloatMax = 10f, EditorSize = 300)]
            public float ThrottleF = 0.1f;

            [FeatureConfigOption("仅在副本内使用")]
            public bool OnlyInDuty = false;

            [FeatureConfigOption("在行走状态时使用")]
            public bool RPWalk = false;

            [FeatureConfigOption("在住宅区禁用")]
            public bool ExcludeHousing = false;

            [FeatureConfigOption("在倒计时期间中止使用")]
            public bool AbortCooldown = false;
        }

        public Configs Config { get; private set; }

        public override void Enable()
        {
            Config = LoadConfig<Configs>() ?? new Configs();
            Svc.Framework.Update += RunFeature;
            base.Enable();
        }

        private void RunFeature(IFramework framework)
        {
            if (Svc.ClientState.LocalPlayer == null) return;

            if (IsRpWalking() && !Config.RPWalk) return;
            if (Svc.Condition[ConditionFlag.InCombat]) return;
            if (Svc.ClientState.LocalPlayer is null) return;
            var r = new Regex("/hou/|/ind/");
            if (r.IsMatch(Svc.Data.GetExcelSheet<TerritoryType>().GetRow(Svc.ClientState.TerritoryType).Bg.RawString) && Config.ExcludeHousing) return;

            if (Config.AbortCooldown && Countdown.TimeRemaining() > 0)
            {
                TaskManager.Abort();
                return;
            }

            var am = ActionManager.Instance();
            var isPeletonReady = am->GetActionStatus(ActionType.Action, 7557) == 0;
            var hasPeletonBuff = Svc.ClientState.LocalPlayer.StatusList.Any(x => x.StatusId == 1199 || x.StatusId == 50);

            if (isPeletonReady && !hasPeletonBuff && AgentMap.Instance()->IsPlayerMoving == 1 && !TaskManager.IsBusy)
            {
                TaskManager.Enqueue(() => EzThrottler.Throttle("Pelotoning", (int)(Config.ThrottleF * 1000)));
                TaskManager.Enqueue(() => EzThrottler.Check("Pelotoning"));
                TaskManager.Enqueue(UsePeloton);
            }
        }

        private void UsePeloton()
        {
            if (IsRpWalking() && !Config.RPWalk) return;
            if (Svc.Condition[ConditionFlag.InCombat]) return;
            if (Svc.ClientState.LocalPlayer is null) return;
            if (Config.OnlyInDuty && GameMain.Instance()->CurrentContentFinderConditionId == 0) return;

            var am = ActionManager.Instance();
            var isPeletonReady = am->GetActionStatus(ActionType.Action, 7557) == 0;
            var hasPeletonBuff = Svc.ClientState.LocalPlayer.StatusList.Any(x => x.StatusId == 1199 || x.StatusId == 50);

            if (isPeletonReady && !hasPeletonBuff && AgentMap.Instance()->IsPlayerMoving == 1)
            {
                am->UseAction(ActionType.Action, 7557);
            }
        }


        public override void Disable()
        {
            Svc.Framework.Update -= RunFeature;
            SaveConfig(Config);
            base.Disable();
        }

        protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) =>
        {
            ImGui.PushItemWidth(300);
            if (ImGui.SliderFloat("设置延迟 (秒)", ref Config.ThrottleF, 0.1f, 10f, "%.1f")) hasChanged = true;
            if (ImGui.Checkbox("仅在副本内使用", ref Config.OnlyInDuty)) hasChanged = true;
            if (ImGui.Checkbox("在行走状态时使用", ref Config.RPWalk)) hasChanged = true;
            if (ImGui.Checkbox("在住宅区禁用", ref Config.ExcludeHousing)) hasChanged = true;
            if (ImGui.Checkbox($"在倒计时期间中止使用", ref Config.AbortCooldown)) hasChanged = true;
        };
    }
}
