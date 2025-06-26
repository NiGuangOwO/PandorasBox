using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using PandorasBox.FeaturesSetup;
using System.Linq;

namespace PandorasBox.Features.Actions
{
    public unsafe class AutoProspectTriangulate : Feature
    {
        public override string Name => "自动矿脉勘探/三角测量";

        public override string Description => "当切换到采矿工或园艺工时，自动激活另一个职业的搜索技能。";

        public override FeatureType FeatureType => FeatureType.Actions;

        public override bool UseAutoConfig => true;

        public class Configs : FeatureConfig
        {
            [FeatureConfigOption("设置延迟 (秒)", FloatMin = 0.1f, FloatMax = 10f, EditorSize = 300)]
            public float ThrottleF = 0.1f;

            [FeatureConfigOption("包括山岳之相/丛林之相", "", 1)]
            public bool AddTruth = false;
        }

        public Configs Config { get; private set; } = null!;


        private void ActivateBuff(uint? jobValue)
        {
            if (jobValue is null) return;
            if (jobValue is not (16 or 17)) return;
            if (Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas]) return;
            TaskManager.EnqueueDelay((int)(Config.ThrottleF * 1000));
            var am = ActionManager.Instance();   
            if (Svc.ClientState.LocalPlayer?.StatusList.Where(x => x.StatusId == 217 || x.StatusId == 225).Count() == 2)
                return;
            if (Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.Gathering])
            {
                TaskManager.Abort();
                return;
            }

            if (jobValue == 16 && am->GetActionStatus(ActionType.Action, 210) == 0)
            {
                TaskManager.Enqueue(() => am->UseAction(ActionType.Action, 210));
                if (Config.AddTruth && am->GetActionStatus(ActionType.Action, 221) == 0)
                {
                   TaskManager.Enqueue(() => am->UseAction(ActionType.Action, 221));
                }
                return;
            }
            if (jobValue == 17 && am->GetActionStatus(ActionType.Action, 227) == 0)
            {
                TaskManager.Enqueue(() => am->UseAction(ActionType.Action, 227));
                if (Config.AddTruth && am->GetActionStatus(ActionType.Action, 238) == 0)
                {
                    TaskManager.Enqueue(() => am->UseAction(ActionType.Action, 238));
                }
                return;
            }

        }

        public override void Enable()
        {
            Config = LoadConfig<Configs>() ?? new Configs();
            Events.OnJobChanged += ActivateBuff;
            base.Enable();
        }

        public override void Disable()
        {
            SaveConfig(Config);
            Events.OnJobChanged -= ActivateBuff;
            base.Disable();
        }
    }
}
