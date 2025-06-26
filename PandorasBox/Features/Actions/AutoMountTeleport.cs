using Dalamud.Game.ClientState.Conditions;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using ImGuiNET;
using Lumina.Excel.Sheets;
using PandorasBox.FeaturesSetup;
using PandorasBox.Helpers;
using System.Linq;
using PlayerState = FFXIVClientStructs.FFXIV.Client.Game.UI.PlayerState;

namespace PandorasBox.Features.Actions
{
    public unsafe class AutoMountZoneChange : Feature
    {
        public override string Name => "过图后自动上坐骑";

        public override string Description => "如果没有在坐骑上，则在过图后自动上坐骑。";

        public override FeatureType FeatureType => FeatureType.Actions;

        public class Configs : FeatureConfig
        {
            public float ThrottleF = 0.1f;
            public uint SelectedMount = 0;
            public bool ExcludeHousing = false;
            public bool JumpAfterMount = false;
        }

        public Configs Config { get; private set; } = null!;

        public override bool UseAutoConfig => false;

        public override void Enable()
        {
            Config = LoadConfig<Configs>() ?? new Configs();
            Svc.ClientState.TerritoryChanged += RunFeature;
            base.Enable();
        }

        private void RunFeature(ushort e)
        {
            if (!Svc.Data.GetExcelSheet<TerritoryType>().First(x => x.RowId == e).Mount) return;

            if (Svc.Data.GetExcelSheet<TerritoryType>().First(x => x.RowId == e).Bg.ToString().Contains("/hou/") && Config.ExcludeHousing)
            {
                TaskManager.Abort();
                return;
            }
            TaskManager.Enqueue(() => NotBetweenAreas);
            TaskManager.EnqueueDelay((int)(Config.ThrottleF * 1000));
            TaskManager.EnqueueWithTimeout(TryMount, 3000);
            TaskManager.Enqueue(() =>
            {
                if (Config.JumpAfterMount && ZoneHasFlight())
                {
                    TaskManager.EnqueueWithTimeout(() => Svc.Condition[ConditionFlag.Mounted], 5000);
                    TaskManager.EnqueueDelay(50);
                    TaskManager.Enqueue(() => ActionManager.Instance()->UseAction(ActionType.GeneralAction, 2));
                    TaskManager.EnqueueDelay(50);
                    TaskManager.Enqueue(() => ActionManager.Instance()->UseAction(ActionType.GeneralAction, 2));
                }
            });
        }

        private static bool NotBetweenAreas => !Svc.Condition[ConditionFlag.BetweenAreas];
        private bool? TryMount()
        {
            if (Svc.ClientState.LocalPlayer is null) return false;
            if (Svc.Condition[ConditionFlag.BetweenAreas] || Svc.Condition[ConditionFlag.BetweenAreas51]) return false;
            if (Svc.Condition[ConditionFlag.Mounted]) return true;

            var am = ActionManager.Instance();

            if (Config.SelectedMount > 0)
            {
                if (am->GetActionStatus(ActionType.Mount, Config.SelectedMount) != 0) return false;
                am->UseAction(ActionType.Mount, Config.SelectedMount);

                return true;
            }
            else
            {
                if (am->GetActionStatus(ActionType.GeneralAction, 9) != 0) return false;
                am->UseAction(ActionType.GeneralAction, 9);

                return true;
            }

        }

        public override void Disable()
        {
            SaveConfig(Config);
            Svc.ClientState.TerritoryChanged -= RunFeature;
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

            if (ImGui.Checkbox("在房区内禁用", ref Config.ExcludeHousing)) hasChanged = true;
            if (ImGui.Checkbox("上坐骑后自动跳跃", ref Config.JumpAfterMount)) hasChanged = true;

        };
    }
}
