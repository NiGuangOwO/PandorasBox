using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using ECommons.Automation;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using PandorasBox.FeaturesSetup;
using System;
using System.Runtime.InteropServices;
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
            [FeatureConfigOption("在剩余多少分钟时进行续期", IntMin = 1, IntMax = 55, EditorSize = 300)]
            public int ThrottleF = 10;
            [FeatureConfigOption("在编辑招募内容界面点击更新招募内容按钮延迟 (秒)", FloatMin = 1f, FloatMax = 5f, EditorSize = 300)]
            public float ClickUpdateThrottleF = 1f;
        }
        public Configs Config { get; private set; }

        private DateTime UpdateTime = DateTime.MinValue;
        private DateTime RunningTime = DateTime.MinValue;
        private bool isRunning;

        internal delegate byte OpenPartyFinderInfoDelegate(void* agentLfg, ulong contentId);
        internal OpenPartyFinderInfoDelegate? OpenPartyFinderInfo;

        private void Logout()
        {
            TaskManager.Abort();
            UpdateTime = DateTime.MinValue;
            RunningTime = DateTime.MinValue;
            isRunning = false;
        }

        private void RunFeature(IFramework framework)
        {
            if (UpdateTime == DateTime.MinValue)
                return;
            if (!Player.Available)
                return;
            if (isRunning && DateTime.Now > RunningTime.AddMinutes(1))
            {
                TaskManager.Abort();
                isRunning = false;
                var message = new XivChatEntry
                {
                    Message = new SeStringBuilder()
                    .AddUiForeground($"[{P.Name}] ", 45)
                    .AddUiForeground($"{Name} ", 62)
                    .AddText("自动续招募失败，正在重试...")
                    .Build(),
                };
                Svc.Chat.Print(message);
                return;
            }

            if (DateTime.Now > UpdateTime && !isRunning)
            {
                if (Player.Object.OnlineStatus.Id != 26)
                {
                    UpdateTime = DateTime.MinValue;
                    var msg = new XivChatEntry
                    {
                        Message = new SeStringBuilder()
                        .AddUiForeground($"[{P.Name}] ", 45)
                        .AddUiForeground($"{Name} ", 62)
                        .AddText("没有进行的招募，已自动停止招募自动续期。")
                        .Build(),
                    };
                    Svc.Chat.Print(msg);
                    return;
                }

                isRunning = true;
                RunningTime = DateTime.Now;
                TaskManager.Enqueue(() => OpenSelfPF(), "打开自己招募");
                TaskManager.Enqueue(() => ClickChange(), "点击更改");
                var message = new XivChatEntry
                {
                    Message = new SeStringBuilder()
                    .AddUiForeground($"[{P.Name}] ", 45)
                    .AddUiForeground($"{Name} ", 62)
                    .AddText($"开始尝试自动续招募，如果发生错误将在1分钟后自动重试。")
                    .Build(),
                };
                Svc.Chat.Print(message);
            }
        }

        internal bool OpenSelfPF()
        {
            if (Player.Available)
            {
                if (GenericThrottle)
                {
                    OpenPartyFinderInfoDetour(AgentLookingForGroup.Instance(), Player.CID);
                    return true;
                }
            }
            return false;
        }

        private static bool ClickChange()
        {
            if (TryGetAddonMaster<AddonMaster.LookingForGroupDetail>(out var m) && m.IsAddonReady)
            {
                if (GenericThrottle)
                {
                    m.JoinEdit();
                    return true;
                }
            }
            return false;
        }

        private static bool ClosePF()
        {
            if (TryGetAddonMaster<AddonMaster.LookingForGroup>(out var m) && m.IsAddonReady)
            {
                if (GenericThrottle)
                {
                    Chat.Instance.ExecuteCommand("/pfinder");
                    return true;
                }
            }
            return false;
        }

        private void CheckMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
        {
            if (type == XivChatType.SystemMessage)
            {
                if (message.TextValue.Contains("开始招募队员"))
                {
                    TaskManager.Abort();
                    UpdateTime = DateTime.Now.AddMinutes(60 - Config.ThrottleF);
                    var msg = new XivChatEntry
                    {
                        Message = new SeStringBuilder()
                        .AddUiForeground($"[{P.Name}] ", 45)
                        .AddUiForeground($"{Name} ", 62)
                        .AddText($"已开启招募自动续期，将在 {UpdateTime} 尝试自动续期")
                        .Build(),
                    };
                    Svc.Chat.Print(msg);
                }
                else if (message.TextValue.Contains("招募队员结束"))
                {
                    TaskManager.Abort();
                    UpdateTime = DateTime.MinValue;
                    RunningTime = DateTime.MinValue;
                    isRunning = false;
                    var msg = new XivChatEntry
                    {
                        Message = new SeStringBuilder()
                        .AddUiForeground($"[{P.Name}] ", 45)
                        .AddUiForeground($"{Name} ", 62)
                        .AddText("招募关闭，已自动停止招募自动续期。")
                        .Build(),
                    };
                    Svc.Chat.Print(msg);
                }
            }

            if ((int)type == 2105 && message.TextValue == "招募队员已撤销。" && isRunning)
            {
                TaskManager.DelayNext("点击更新招募内容按钮延迟", (int)(Config.ClickUpdateThrottleF * 1000));
                TaskManager.Enqueue(() => ClickUpdate(), "点击更新");
                TaskManager.Enqueue(() => ClosePF(), "关闭招募板");
                TaskManager.Enqueue(() => { isRunning = false; });
            }
        }

        private bool ClickUpdate()
        {
            if (TryGetAddonMaster<AddonMaster.LookingForGroupCondition>(out var m) && IsAddonReady(m.Base))
            {
                if (GenericThrottle)
                {
                    m.Recruit();
                    UpdateTime = DateTime.Now.AddMinutes(60 - Config.ThrottleF);
                    var message = new XivChatEntry
                    {
                        Message = new SeStringBuilder()
                        .AddUiForeground($"[{P.Name}] ", 45)
                        .AddUiForeground($"{Name} ", 62)
                        .AddText($"招募自动续期已完成，将在 {UpdateTime} 进行下一次续期")
                        .Build(),
                    };
                    Svc.Chat.Print(message);
                    return true;
                }
            }
            return false;
        }


        public override void Enable()
        {
            Config = LoadConfig<Configs>() ?? new Configs();
            var OpenPartyFinderInfoAddress = Svc.SigScanner.ScanText("40 53 48 83 EC 20 48 8B D9 E8 ?? ?? ?? ?? 84 C0 74 07 C6 83 ?? ?? ?? ?? ?? 48 83 C4 20 5B C3 CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC 40 53");
            OpenPartyFinderInfo = Marshal.GetDelegateForFunctionPointer<OpenPartyFinderInfoDelegate>(OpenPartyFinderInfoAddress);
            Svc.ClientState.Logout += Logout;
            Svc.Chat.CheckMessageHandled += CheckMessage;
            Svc.Framework.Update += RunFeature;
            base.Enable();
        }

        public override void Disable()
        {
            SaveConfig(Config);
            Svc.ClientState.Logout -= Logout;
            Svc.Chat.CheckMessageHandled -= CheckMessage;
            Svc.Framework.Update -= RunFeature;
            base.Disable();
        }
        private void OpenPartyFinderInfoDetour(void* agentLfg, ulong contentId) => OpenPartyFinderInfo!.Invoke(agentLfg, contentId);
    }
}
