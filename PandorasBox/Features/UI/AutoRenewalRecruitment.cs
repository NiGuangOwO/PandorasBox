using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using ECommons.Automation;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using PandorasBox.FeaturesSetup;
using System;
using static ECommons.GenericHelpers;


namespace PandorasBox.Features
{
    public unsafe class AutoRenewalRecruitment : Feature
    {
        public override string Name => "[国服限定] 招募自动续期";

        public override string Description => "当招募剩余时间不足时自动续期。";

        public override FeatureType FeatureType => FeatureType.UI;

        public override bool UseAutoConfig => true;

        public class Configs : FeatureConfig
        {
            [FeatureConfigOption("剩余多少分钟时自动续期", IntMin = 1, IntMax = 59, EditorSize = 300)]
            public int ThrottleF = 10;
        }

        public Configs Config { get; private set; }

        private DateTime StartTime { get; set; } = DateTime.MinValue;
        private bool isRunning { get; set; } = false;

        private void CheckCondition(ConditionFlag flag, bool value)
        {
            if (flag == ConditionFlag.UsingPartyFinder && value)
            {
                StartTime = DateTime.Now;
                var message = new XivChatEntry
                {
                    Message = new SeStringBuilder()
                .AddUiForeground($"[{P.Name}] ", 45)
                .AddUiForeground($"{Name} ", 62)
                .AddText("已开启招募自动续期")
                .Build(),
                };

                Svc.Chat.Print(message);
            }

            if (flag is ConditionFlag.UsingPartyFinder or ConditionFlag.LoggingOut && !value)
            {
                StartTime = DateTime.MinValue;
                isRunning = false;
            }
        }

        private void RunFeature(IFramework framework)
        {
            if (StartTime == DateTime.MinValue)
                return;

            if ((DateTime.Now - StartTime).TotalMinutes >= 60 - Config.ThrottleF)
            {
                if (isRunning)
                    return;
                isRunning = true;
                TaskManager.Enqueue(() => OpenPF(), "打开招募板");
                TaskManager.Enqueue(() => OpenLookingForGroupDetail(), "打开招募详情");
                TaskManager.Enqueue(() => ClickChange(), "点击更改");
            }
        }

        private bool OpenPF()
        {
            if (Svc.GameGui.GetAddonByName("LookingForGroup") != IntPtr.Zero)
                return true;
            if (Svc.Condition[ConditionFlag.BetweenAreas])
                return false;
            Chat.Instance.SendMessage("/partyfinder");
            return Svc.GameGui.GetAddonByName("LookingForGroup") != IntPtr.Zero && ((AtkUnitBase*)Svc.GameGui.GetAddonByName("LookingForGroup"))->IsVisible;
        }

        private static bool OpenLookingForGroupDetail()
        {
            if (TryGetAddonByName<AddonLookingForGroupDetail>("LookingForGroupDetail", out var addon))
            {
                return addon->AtkUnitBase.IsVisible;
            }
            if (Svc.GameGui.GetAddonByName("LookingForGroup") != IntPtr.Zero)
            {
                var addon2 = (AtkUnitBase*)Svc.GameGui.GetAddonByName("LookingForGroup");
                Callback.Fire(addon2, true, 14);
                return true;
            }
            return false;
        }

        private static bool ClickChange()
        {
            if (TryGetAddonByName<AddonLookingForGroupDetail>("LookingForGroupDetail", out var addon))
            {
                Callback.Fire((AtkUnitBase*)addon, true, 0);
                return true;
            }
            return false;
        }

        private bool ClickUpdate()
        {
            if (Svc.GameGui.GetAddonByName("LookingForGroupCondition") != IntPtr.Zero)
            {
                var addon = (AtkUnitBase*)Svc.GameGui.GetAddonByName("LookingForGroupCondition");
                Callback.Fire(addon, true, 0);
                StartTime = DateTime.Now;
                var message = new XivChatEntry
                {
                    Message = new SeStringBuilder()
                .AddUiForeground($"[{P.Name}] ", 45)
                .AddUiForeground($"{Name} ", 62)
                .AddText("招募自动续期已完成")
                .Build(),
                };

                Svc.Chat.Print(message);
                return true;
            }
            return false;
        }

        private static bool ClosePF()
        {
            if (Svc.GameGui.GetAddonByName("LookingForGroup") != IntPtr.Zero)
            {
                var addon = (AtkUnitBase*)Svc.GameGui.GetAddonByName("LookingForGroup");
                Callback.Fire(addon, true, -1);
                return true;
            }
            return false;
        }

        private void CheckMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
        {
            if (!isRunning)
                return;
            if ((int)type == 2105 && message.TextValue == "招募队员已撤销。")
            {
                TaskManager.Enqueue(() => ClickUpdate(), "点击更新");
                TaskManager.Enqueue(() => ClosePF(), "关闭招募板");
                TaskManager.Enqueue(() => { isRunning = false; });
            }
        }

        public override void Enable()
        {
            Config = LoadConfig<Configs>() ?? new Configs();
            Svc.Condition.ConditionChange += CheckCondition;
            Svc.Chat.CheckMessageHandled += CheckMessage;
            Svc.Framework.Update += RunFeature;
            base.Enable();
        }

        public override void Disable()
        {
            SaveConfig(Config);
            Svc.Framework.Update -= RunFeature;
            Svc.Chat.CheckMessageHandled -= CheckMessage;
            Svc.Condition.ConditionChange -= CheckCondition;
            base.Disable();
        }
    }
}
