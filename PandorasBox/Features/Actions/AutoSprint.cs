using ECommons.DalamudServices;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.MJI;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using PandorasBox.FeaturesSetup;
using System.Linq;
using System.Text.RegularExpressions;

namespace PandorasBox.Features.Actions
{
    public unsafe class AutoSprint : Feature
    {
        public override string Name => "休息区自动冲刺";

        public override string Description => "当你在例如主城获得休息区经验时，自动使用冲刺。";

        public override FeatureType FeatureType => FeatureType.Actions;

        public override bool UseAutoConfig => false;

        public class Configs : FeatureConfig
        {
            public float ThrottleF = 0.1f;
            public bool RPWalk = false;
            public bool ExcludeHousing = false;
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

            if (!TerritoryInfo.Instance()->InSanctuary || MJIManager.Instance()->IsPlayerInSanctuary == 1)
                return;

            var r = new Regex("/hou/|/ind/");
            var loc = Svc.Data.GetExcelSheet<TerritoryType>().GetRow(Svc.ClientState.TerritoryType).Bg.RawString;
            if (r.IsMatch(loc) && Config.ExcludeHousing) return;

            if (IsRpWalking() && !Config.RPWalk)
                return;

            var am = ActionManager.Instance();
            var isSprintReady = am->GetActionStatus(ActionType.GeneralAction, 4) == 0;
            var hasSprintBuff = Svc.ClientState.LocalPlayer?.StatusList.FirstOrDefault(x => x.StatusId == 50) != default;

            if (isSprintReady && !hasSprintBuff && AgentMap.Instance()->IsPlayerMoving == 1 && !TaskManager.IsBusy)
            {
                TaskManager.Enqueue(() => EzThrottler.Throttle("Sprinting", (int)(Config.ThrottleF * 1000)));
                TaskManager.Enqueue(() => EzThrottler.Check("Sprinting"));
                TaskManager.Enqueue(UseSprint);
            }
        }

        private void UseSprint()
        {
            var am = ActionManager.Instance();
            var isSprintReady = am->GetActionStatus(ActionType.GeneralAction, 4) == 0;
            var hasSprintBuff = Svc.ClientState.LocalPlayer?.StatusList.Any(x => x.StatusId == 50);

            if (isSprintReady && AgentMap.Instance()->IsPlayerMoving == 1)
            {
                am->UseAction(ActionType.GeneralAction, 4);
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
            if (ImGui.Checkbox("在行走状态时使用", ref Config.RPWalk)) hasChanged = true;
            if (ImGui.Checkbox("在房区内禁用", ref Config.ExcludeHousing)) hasChanged = true;
        };
    }
}
