using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.MJI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using PandorasBox.FeaturesSetup;
using System.Linq;

namespace PandorasBox.Features
{
    public unsafe class SanctuarySprint : Feature
    {
        public override string Name => "无人岛自动冲刺";

        public override string Description => "自动使用海岛冲刺。";

        public override FeatureType FeatureType => FeatureType.Actions;

        public Configs Config { get; private set; }
        public override bool UseAutoConfig => true;

        public class Configs : FeatureConfig
        {
            [FeatureConfigOption("在行走状态时使用", "", 1)]
            public bool RPWalk = false;
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

        private void RunFeature(IFramework framework)
        {
            if (MJIManager.Instance()->IsPlayerInSanctuary == 0)
                return;

            if (IsRpWalking() && !Config.RPWalk)
                return;

            var am = ActionManager.Instance();
            var isSprintReady = am->GetActionStatus(ActionType.Action, 31314) == 0;
            var hasBuff = Svc.ClientState.LocalPlayer.StatusList.Any(x => x.StatusId == 50 && x.RemainingTime >= 1f);

            if (isSprintReady && !hasBuff && AgentMap.Instance()->IsPlayerMoving == 1)
                am->UseAction(ActionType.Action, 31314);

        }
    }
}
