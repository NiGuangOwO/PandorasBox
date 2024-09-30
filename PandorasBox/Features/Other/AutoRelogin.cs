using Dalamud.Interface.ImGuiNotification;
using ECommons.Automation;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using PandorasBox.FeaturesSetup;
using PandorasBox.Helpers;
using System;

namespace PandorasBox.Features.Other
{
    public unsafe class AutoRelogin : Feature
    {
        public override string Name => "[国服限定] 掉线自动重登";

        public override string Description => "在掉线时实现自动重新上线";

        public override FeatureType FeatureType => FeatureType.Other;

        public override bool UseAutoConfig => false;

        public Configs Config { get; private set; }

        public class Configs : FeatureConfig
        {
            public uint CharacterSlot = 0;
            public System.Windows.Forms.Keys Key = System.Windows.Forms.Keys.NumPad0;
        }

        protected override DrawConfigDelegate DrawConfigTree => (ref bool _) =>
        {
            ImGui.Text("如果你的常用角色不是第一个，请在这修改");
            if (ImGui.BeginCombo("角色", $"角色#{Config.CharacterSlot + 1}"))
            {
                for (uint i = 0; i < 8; i++)
                {
                    if (ImGui.Selectable($"角色#{i + 1}", Config.CharacterSlot == i))
                    {
                        Config.CharacterSlot = i;
                        SaveConfig(Config);
                    }
                }
                ImGui.EndCombo();
            }
            if (KeyHelper.KeyInput("如果你修改了默认键盘确认按键，即非小键盘数字0，请在这修改", ref Config.Key))
            {
                SaveConfig(Config);
            }
        };

        private bool logging = false;

        private void CheckLogin(IFramework framework)
        {
            if (Svc.ClientState.IsLoggedIn)
            {
                logging = false;
                return;
            }

            if (Svc.KeyState[Dalamud.Game.ClientState.Keys.VirtualKey.SHIFT] && logging)
            {
                logging = false;
                TaskManager.Abort();
                Svc.NotificationManager.AddNotification(new Notification { Title = "Pandoras", Content = "自动重登已取消", Type = NotificationType.Warning });
                return;
            }
        }

        private void CheckLogout()
        {
            if ((AtkUnitBase*)Svc.GameGui.GetAddonByName("Dialogue") == null)
                return;
            TaskManager.Enqueue(() => CheckTitle(), int.MaxValue, "CheckTitle");
            TaskManager.Enqueue(() => ClickStart(), "开始游戏");
            TaskManager.Enqueue(() => Message(), "发送提示");
            TaskManager.Enqueue(() => SelectCharacter(), int.MaxValue, "选择角色");
            TaskManager.Enqueue(() => SelectYes(), "点击确定");
        }

        private void CheckDialogue(IFramework framework)
        {
            if (Svc.GameGui.GetAddonByName("Dialogue") != IntPtr.Zero && !Svc.Condition.Any())
            {
                var addon = (AtkUnitBase*)Svc.GameGui.GetAddonByName("Dialogue");
                if (!addon->IsVisible)
                    return;

                WindowsKeypress.SendKeypress(Config.Key);
            }
        }

        public bool CheckTitle()
        {
            return (AtkUnitBase*)Svc.GameGui.GetAddonByName("_TitleMenu") != null
                && ((AtkUnitBase*)Svc.GameGui.GetAddonByName("_TitleMenu"))->IsVisible;
        }

        public bool Message()
        {
            logging = true;
            Svc.NotificationManager.AddNotification(new Notification { Title = "Pandoras", Content = "开始自动重登,按Shift中止", Type = NotificationType.Info });
            return true;
        }

        public bool ClickStart()
        {
            var addon = (AtkUnitBase*)Svc.GameGui.GetAddonByName("_TitleMenu");
            if (addon == null)
                return false;
            if (!addon->IsVisible)
                return false;
            Callback.Fire(addon, true, 4);
            return true;
        }

        public bool SelectCharacter()
        {
            var addon = (AtkUnitBase*)Svc.GameGui.GetAddonByName("_CharaSelectListMenu");
            if (addon == null)
                return false;
            Callback.Fire(addon, true, 29, 0, Config.CharacterSlot);
            var nextAddon = (AtkUnitBase*)Svc.GameGui.GetAddonByName("SelectYesno");
            return nextAddon != null;
        }

        public bool SelectYes()
        {
            var addon = (AtkUnitBase*)Svc.GameGui.GetAddonByName("SelectYesno");
            if (addon == null)
                return false;
            Callback.Fire(addon, true, 0);
            return true;
        }

        public override void Enable()
        {
            Config = LoadConfig<Configs>() ?? new Configs();
            Svc.ClientState.Logout += CheckLogout;
            Svc.Framework.Update += CheckLogin;
            Svc.Framework.Update += CheckDialogue;
            base.Enable();
        }

        public override void Disable()
        {
            SaveConfig(Config);
            Svc.ClientState.Logout -= CheckLogout;
            Svc.Framework.Update -= CheckLogin;
            Svc.Framework.Update -= CheckDialogue;
            base.Disable();
        }
    }
}
