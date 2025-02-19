using ECommons.Automation;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Component.GUI;
using PandorasBox.FeaturesSetup;
using System;
using static ECommons.GenericHelpers;

namespace PandorasBox.Features.UI
{
    internal class RefreshPF : Feature
    {
        public override string Name { get; } = "自动招募板刷新";
        public override string Description { get; } = "按设定的时间间隔刷新招募板";

        public class Config : FeatureConfig
        {
            [FeatureConfigOption("刷新间隔（秒）", IntMin = 2, IntMax = 60, EditorSize = 300)]
            public int Refresh = 10;
        }

        public Config Configs { get; private set; }

        public override bool UseAutoConfig => true;

        public override FeatureType FeatureType => FeatureType.UI;

        private long ThrottleTime = Environment.TickCount64;

        public override void Enable()
        {
            Configs = LoadConfig<Config>() ?? new Config();
            Svc.Framework.Update += RunFeature;
            base.Enable();
        }

        private unsafe void RunFeature(IFramework framework)
        {
            if (TryGetAddonByName<AtkUnitBase>("LookingForGroup", out var addon) && addon is not null && addon->IsVisible)
            {
                var refreshBtn = addon->UldManager.SearchNodeById(47)->GetAsAtkComponentButton();

                if (Environment.TickCount64 >= ThrottleTime)
                {
                    ThrottleTime = Environment.TickCount64 + (Configs.Refresh * 1000);
                    Callback.Fire(addon, true, 17);

                }
            }
        }

        public override void Disable()
        {
            SaveConfig(Configs);
            Svc.Framework.Update -= RunFeature;
            base.Disable();
        }
    }
}
