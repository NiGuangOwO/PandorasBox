using Dalamud.Game.ClientState.Conditions;
using ECommons.Automation;
using ECommons.DalamudServices;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.MJI;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using ImGuiNET;
using Lumina.Excel.Sheets;
using PandorasBox.FeaturesSetup;
using PandorasBox.Helpers;
using System.Linq;

namespace PandorasBox.Features.Actions
{
    public unsafe class AutoMountGathering : Feature
    {
        public override string Name => "采集后自动上坐骑";

        public override string Description => "在采集点完成采集后使用随机坐骑或特定坐骑。如果在移动，则会在3秒内持续尝试上坐骑。";

        public override FeatureType FeatureType => FeatureType.Actions;

        public class Configs : FeatureConfig
        {
            public float ThrottleF = 0.1f;
            public uint SelectedMount = 0;
            public bool UseOnIsland = false;
            public bool JumpAfterMount = false;
            public bool MoveAfterMount = false;
        }

        public Configs Config { get; private set; } = null!;

        public override bool UseAutoConfig => false;
        public override void Enable()
        {
            Config = LoadConfig<Configs>() ?? new Configs();
            Svc.Condition.ConditionChange += RunFeature;
            base.Enable();
        }

        private bool GatheredOnIsland(ConditionFlag flag, bool value)
        {
            return flag == ConditionFlag.OccupiedInQuestEvent && !value && MJIManager.Instance()->IsPlayerInSanctuary != 0;
        }

        private void RunFeature(ConditionFlag flag, bool value)
        {
            if (flag == ConditionFlag.Gathering && !value || (GatheredOnIsland(flag, value) && Config.UseOnIsland))
            {
                TaskManager.Enqueue(() => EzThrottler.Throttle("GatherMount", (int)(Config.ThrottleF * 1000)));
                TaskManager.Enqueue(() => EzThrottler.Check("GatherMount"));
                TaskManager.EnqueueWithTimeout(TryMount, 3000);
                TaskManager.Enqueue(() =>
                {
                    Svc.GameConfig.TryGet(Dalamud.Game.Config.UiControlOption.FlyingControlType, out uint type);
                    if (Config.JumpAfterMount && Svc.ClientState.LocalPlayer!.ClassJob.RowId != 18 && ZoneHasFlight())
                    {
                        TaskManager.EnqueueWithTimeout(() => Svc.Condition[ConditionFlag.Mounted], 5000);
                        TaskManager.EnqueueDelay(50);
                        TaskManager.Enqueue(() => ActionManager.Instance()->UseAction(ActionType.GeneralAction, 2));
                        if (type == 1)
                        {
                            TaskManager.EnqueueDelay(50);
                            TaskManager.Enqueue(() => ActionManager.Instance()->UseAction(ActionType.GeneralAction, 2));
                        }
                    }

                    if (Config.MoveAfterMount)
                    {
                        TaskManager.EnqueueWithTimeout(() => Svc.Condition[ConditionFlag.Mounted], 5000);
                        TaskManager.EnqueueWithTimeout(() => Svc.Condition[ConditionFlag.InFlight] || Svc.Condition[ConditionFlag.Diving], 3000);
                        TaskManager.Enqueue(() => { Chat.Instance.SendMessage("/automove on"); });
                    }
                });
            }
        }

        private bool? TryMount()
        {
            var am = ActionManager.Instance();

            if (Config.SelectedMount > 0)
            {
                if (am->GetActionStatus(ActionType.Mount, Config.SelectedMount) != 0) return false;
                TaskManager.Enqueue(() => am->UseAction(ActionType.Mount, Config.SelectedMount));

                return true;
            }
            else
            {
                if (am->GetActionStatus(ActionType.GeneralAction, 9) != 0) return false;
                TaskManager.Enqueue(() => am->UseAction(ActionType.GeneralAction, 9));

                return true;
            }
        }

        public override void Disable()
        {
            SaveConfig(Config);
            Svc.Condition.ConditionChange -= RunFeature;
            base.Disable();
        }

        protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) =>
        {
            ImGui.PushItemWidth(300);
            if (ImGui.SliderFloat("设置延迟 (秒)", ref Config.ThrottleF, 0.1f, 10f, "%.1f")) hasChanged = true;
            var ps = PlayerState.Instance();
            var preview = Svc.Data.GetExcelSheet<Mount>().First(x => x.RowId == Config.SelectedMount).Singular.ExtractText().ToTitleCase();
            if (ImGui.BeginCombo("选择坐骑", preview))
            {
                if (ImGui.Selectable("", Config.SelectedMount == 0))
                {
                    Config.SelectedMount = 0;
                    hasChanged = true;
                }

                foreach (var mount in Svc.Data.GetExcelSheet<Mount>().OrderBy(x => x.Singular.ExtractText()))
                {
                    if (ps->IsMountUnlocked(mount.RowId))
                    {
                        var selected = ImGui.Selectable(mount.Singular.ExtractText().ToTitleCase(), Config.SelectedMount == mount.RowId);

                        if (selected)
                        {
                            Config.SelectedMount = mount.RowId;
                            hasChanged = true;
                        }
                    }
                }

                ImGui.EndCombo();
            }

            if (ImGui.Checkbox("在无人岛中使用", ref Config.UseOnIsland)) hasChanged = true;
            if (ImGui.Checkbox("上坐骑后自动跳跃", ref Config.JumpAfterMount)) hasChanged = true;
            hasChanged |= ImGui.Checkbox("上坐骑后自动移动", ref Config.MoveAfterMount);
        };
    }
}
