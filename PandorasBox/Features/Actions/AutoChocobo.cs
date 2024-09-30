using PandorasBox.FeaturesSetup;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Dalamud.Game.ClientState.Conditions;

namespace PandorasBox.Features.Actions
{
    public unsafe class AutoChocobo : Feature
    {
        public override string Name => "自动召唤陆行鸟";

        public override string Description => "如果你在野外且没有召唤陆行鸟，则自动消耗一个基萨尔野菜来召唤陆行鸟。";

        public override FeatureType FeatureType => FeatureType.Actions;

        public Configs Config { get; private set; }

        public override bool UseAutoConfig => true;

        public class Configs : FeatureConfig
        {
            [FeatureConfigOption("设置重新召唤的剩余时间 (秒)", IntMin = 0, IntMax = 600, EditorSize = 300)]
            public int RemainingTimeLimit = 0;

            [FeatureConfigOption("在小队中使用")]
            public bool UseInParty = true;

            [FeatureConfigOption("在战斗中使用")]
            public bool UseInCombat = false;

            [FeatureConfigOption("挂机5分钟后禁止使用")]
            public bool AfkCheck = true;
        }

        private void RunFeature(IFramework framework)
        {
            if (!Svc.Condition[ConditionFlag.NormalConditions] || Svc.Condition[ConditionFlag.Casting] || IsMoving()) return;
            if (Svc.Condition[ConditionFlag.InCombat] && !Config.UseInCombat) return;
            if (Svc.Party.Length > 1 && !Config.UseInParty) return;
            if (AFKTimer.Stopwatch.Elapsed.TotalMinutes >= 5 && Config.AfkCheck) return;

            var am = ActionManager.Instance();
            if (UIState.Instance()->Buddy.CompanionInfo.TimeLeft <= Config.RemainingTimeLimit)
            {
                if (am->GetActionStatus(ActionType.Item, 4868) != 0) return;
                am->UseAction(ActionType.Item, 4868, extraParam: 65535);
            }
        }

        public override void Enable()
        {
            Config = LoadConfig<Configs>() ?? new Configs();
            Svc.Framework.Update += RunFeature;
            base.Enable();
        }

        public override void Disable()
        {
            Svc.Framework.Update -= RunFeature;
            SaveConfig(Config);
            base.Disable();
        }
    }
}
