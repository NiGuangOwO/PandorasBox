using ECommons.Automation;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using PandorasBox.FeaturesSetup;
using PandorasBox.Helpers;
using PandorasBox.UI;
using System;
using System.Numerics;
using static ECommons.GenericHelpers;


namespace PandorasBox.Features
{
    public unsafe class AutoRefreshPF : Feature
    {
        public override string Name => "[国服限定] 招募板自动刷新";

        public override string Description => "为招募板提供自动刷新功能。";

        public override FeatureType FeatureType => FeatureType.UI;

        public override bool UseAutoConfig => true;

        public class Configs : FeatureConfig
        {
            [FeatureConfigOption("刷新间隔（秒）", IntMin = 2, IntMax = 60, EditorSize = 300)]
            public int ThrottleF = 10;
        }

        public Configs Config { get; private set; }

        internal Overlays window;

        private DateTime 上次刷新时间 { get; set; }
        private bool autoRefresh = false;
        private long throttleTime = Environment.TickCount64;

        public override void Enable()
        {
            Config = LoadConfig<Configs>() ?? new Configs();
            window = new(this);
            Svc.Framework.Update += RunFeature;
            base.Enable();
        }

        public override void Draw()
        {
            try
            {
                if (Svc.GameGui.GetAddonByName("LookingForGroup") != IntPtr.Zero)
                {
                    var addon = (AtkUnitBase*)Svc.GameGui.GetAddonByName("LookingForGroup");
                    if (addon == null || !addon->IsVisible)
                        return;
                    var addon2 = (AtkUnitBase*)Svc.GameGui.GetAddonByName("LookingForGroupCondition");
                    if (addon2 != null && addon2->IsVisible)
                        return;

                    var windowNode = addon->UldManager.NodeList[1];
                    var buttonNode = addon->UldManager.NodeList[5];
                    var windowPosition = AtkResNodeHelper.GetNodePosition(windowNode);
                    var buttonPosition = AtkResNodeHelper.GetNodePosition(buttonNode);
                    var scale = AtkResNodeHelper.GetNodeScale(buttonNode);
                    var windowSize = new Vector2(windowNode->Width, windowNode->Height) * scale;
                    var buttonSize = new Vector2(buttonNode->Width, buttonNode->Height) * scale;

                    ImGuiHelpers.ForceNextWindowMainViewport();
                    ImGuiHelpers.SetNextWindowPosRelativeMainViewport(new Vector2(buttonPosition.X, buttonPosition.Y + buttonSize.Y), ImGuiCond.Always);

                    var oldSize = ImGui.GetFont().Scale;
                    ImGui.GetFont().Scale *= scale.X;
                    ImGui.PushFont(ImGui.GetFont());

                    ImGui.Begin($"###AutoRefresh", ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoNavFocus
                        | ImGuiWindowFlags.AlwaysUseWindowPadding | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.AlwaysAutoResize);

                    ImGui.Checkbox("开启自动刷新", ref autoRefresh);
                    if (autoRefresh)
                    {
                        ImGui.Text($"距离刷新还有 {(上次刷新时间.AddSeconds(Config.ThrottleF + 1) - DateTime.Now).Seconds} 秒");
                    }

                    ImGui.End();
                    ImGui.GetFont().Scale = oldSize;
                    ImGui.PopFont();
                }
            }
            catch
            {

            }
        }

        private void RunFeature(IFramework framework)
        {
            if (!autoRefresh)
                return;
            if (TryGetAddonByName<AtkUnitBase>("LookingForGroup", out var addon) && addon is not null && addon->IsVisible)
            {
                var refreshBtn = addon->UldManager.SearchNodeById(47)->GetAsAtkComponentButton();

                if (Environment.TickCount64 >= throttleTime)
                {
                    throttleTime = Environment.TickCount64 + (Config.ThrottleF * 1000);
                    上次刷新时间 = DateTime.Now;
                    Callback.Fire(addon, true, 17);
                }
            }
        }

        public override void Disable()
        {
            SaveConfig(Config);
            P.Ws.RemoveWindow(window);
            Svc.Framework.Update -= RunFeature;
            base.Disable();
        }
    }
}
