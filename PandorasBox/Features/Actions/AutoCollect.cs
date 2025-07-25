using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using PandorasBox.FeaturesSetup;
using System.Linq;

namespace PandorasBox.Features.Actions
{
    public unsafe class AutoCollect : Feature
    {
        public override string Name => "自动开启收藏品采集";

        public override string Description => "当切换到捕鱼人时, 保证收藏品采集处于开启。";

        public override FeatureType FeatureType => FeatureType.Actions;

        public override bool UseAutoConfig => true;

        public class Configs : FeatureConfig
        {
            [FeatureConfigOption("设置延迟 (秒)", FloatMin = 0.1f, FloatMax = 10f, EditorSize = 300)]
            public float ThrottleF = 0.1f;
        }

        public Configs Config { get; private set; } = null!;

        
        private void ActivateBuff(uint? jobValue)
        {
            if (jobValue is null) return;
            if (jobValue is not (18)) return;
            if (Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas]) return;
            TaskManager.EnqueueDelay((int)(Config.ThrottleF * 1000));
            var am = ActionManager.Instance();   
            if (Svc.ClientState.LocalPlayer?.StatusList.Where(x => x.StatusId == 805).Count() == 1)
                return;
            if (Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.Gathering])
            {
                TaskManager.Abort();
                return;
            }

            if (jobValue == 18 && am->GetActionStatus(ActionType.Action, 4101) == 0)
            {
                TaskManager.Enqueue(() => am->UseAction(ActionType.Action, 4101));
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
