using Dalamud;
using ECommons.DalamudServices;
using PandorasBox.FeaturesSetup;

namespace PandorasBox.Features.Other
{
    public unsafe class SkipCutscene : Feature
    {
        public override string Name => "[国服限定] 辍学";

        public override string Description => "主随辍学跳动画";

        public override FeatureType FeatureType => FeatureType.Other;

        private nint offset1;
        private readonly nint offset2;

        public override void Enable()
        {
            offset1 = Svc.SigScanner.ScanText("?? 32 DB EB ?? 48 8B 01");
            SafeMemory.Write<byte>(offset1, 0x2e);
            base.Enable();
        }

        public override void Disable()
        {
            SafeMemory.Write<byte>(offset1, 0x4);
            base.Disable();
        }
    }
}
