using ECommons;
using ECommons.DalamudServices;
using ECommons.GameFunctions;
using FFXIVClientStructs.FFXIV.Client.Game.Fate;
using PandorasBox.FeaturesSetup;
using PandorasBox.Helpers;
using System.Linq;

namespace PandorasBox.Features.Targets
{
    internal class FATETargeting : Feature
    {
        public override string Name { get; } = "FATE目标模式";
        public override string Description { get; } = "当处于FATE并能够参与（等级同步）时，自动选中与FATE相关的敌人。";
        public override FeatureType FeatureType { get; } = FeatureType.Targeting;

        public override void Enable()
        {
            Svc.Framework.Update += Framework_Update;
            base.Enable();
        }

        private unsafe void Framework_Update(IFramework framework)
        {
            var fate = FateManager.Instance();
            if (fate != null && fate->CurrentFate != null && fate->SyncedFateId > 0)
            {
                var tar = Svc.Targets.Target;
                if (tar == null || (tar.Struct()->FateId == 0 && tar.IsHostile()))
                {
                    if (Svc.Objects.OrderBy(x => GameObjectHelper.GetTargetDistance(x)).TryGetFirst(x => x.Struct()->FateId == fate->CurrentFate->FateId && x.IsHostile(), out var newTar))
                    {
                        Svc.Targets.Target = newTar;
                    }
                }
            }
        }

        public override void Disable()
        {
            Svc.Framework.Update -= Framework_Update;
            base.Disable();
        }
    }
}
