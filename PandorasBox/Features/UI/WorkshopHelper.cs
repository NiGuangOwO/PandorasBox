using ClickLib.Clicks;
using Dalamud;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Utility;
using ECommons;
using ECommons.Automation;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using ECommons.Logging;
using FFXIVClientStructs.FFXIV.Client.Game.MJI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using PandorasBox.FeaturesSetup;
using PandorasBox.Helpers;
using PandorasBox.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using static ECommons.GenericHelpers;

namespace PandorasBox.Features.UI
{
    public unsafe class WorkshopHelper : Feature
    {
        public override string Name => "[国服已适配] 工房助手";

        public override string Description => "为无人岛工房添加菜单，以便快速设置您的生产计划。国服已针对腾讯文档无人岛参考作业分享进行适配。";

        public override FeatureType FeatureType => FeatureType.UI;
        private Overlays overlay;
        public static bool ResetPosition = false;

        public Configs Config { get; private set; }
        public override bool UseAutoConfig => true;
        public class Configs : FeatureConfig
        {
            [FeatureConfigOption("执行延迟 (毫秒)", "", 1, IntMin = 0, IntMax = 1000, EditorSize = 300)]
            public int taskDelay = 200;
            [FeatureConfigOption("切换日期后的延迟 (毫秒)", "", 2, IntMin = 0, IntMax = 1000, EditorSize = 300)]
            public int taskAfterCycleSwitchDelay = 500;
            [FeatureConfigOption("打开工房菜单时自动跳转到周期的下一天。", "", 3)]
            public bool OpenNextDay = false;
        }

        internal static (uint Key, string Name, ushort CraftingTime, ushort LevelReq)[] Craftables;
        public static List<Item> PrimarySchedule = [];
        public static List<Item> SecondarySchedule = [];
        public static List<CyclePreset> MultiCycleList = [];

        internal Dictionary<int, bool> Workshops = new() { [0] = false, [1] = false, [2] = false, [3] = false };
        private int currentWorkshop;
        private int maxWorkshops = 4;
        private List<int> Cycles { get; set; } = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14];
        private int selectedCycle = 0;
        private bool isScheduleRest;
        private bool overrideRest;
        private bool autoWorkshopSelect = true;
        private bool overrideExecutionDisable;
        private bool hasOpened;
        private int currentDay;

        internal const int weekendOffset = 5;
        internal const int fortuneOffset = 3;
        internal const int nextDayOffset = 2;

        public class CyclePreset
        {
            public List<Item> PrimarySchedule { get; set; }
            public List<Item> SecondarySchedule { get; set; }
        }

        public class Item
        {
            public uint Key { get; set; }
            public string Name { get; set; }
            public ushort CraftingTime { get; set; }
            public uint UIIndex { get; set; }
            public ushort LevelReq { get; set; }
            public bool InsufficientRank { get; set; }
            public bool OnRestDay { get; set; }

            public override string ToString()
            {
                return $"Key:{Key}|Name:{Name}|CraftingTime:{CraftingTime}|UIIndex:{UIIndex}|LevelReq:{LevelReq}|InsufficientRank:{InsufficientRank}|OnRestDay:{OnRestDay}";
            }
        }

        public override void Draw()
        {
            var workshopWindow = Svc.GameGui.GetAddonByName("MJICraftSchedule", 1);
            if (workshopWindow == IntPtr.Zero)
            {
                hasOpened = false;
                return;
            }
            var addonPtr = (AtkUnitBase*)workshopWindow;
            if (addonPtr == null)
                return;

            if (!hasOpened && Config.OpenNextDay && IsAddonReady(addonPtr))
            {
                OpenCycle(MJIManager.Instance()->CurrentCycleDay + 2);
                hasOpened = true;
            }

            if (addonPtr->UldManager.NodeListCount > 1)
            {
                if (addonPtr->UldManager.NodeList[1]->IsVisible)
                {
                    var node = addonPtr->UldManager.NodeList[1];

                    if (!node->IsVisible)
                        return;

                    var position = AtkResNodeHelper.GetNodePosition(node);
                    var scale = AtkResNodeHelper.GetNodeScale(node);
                    var size = new Vector2(node->Width, node->Height) * scale;
                    var center = new Vector2((position.X + size.X) / 2, (position.Y - size.Y) / 2);

                    ImGuiHelpers.ForceNextWindowMainViewport();
                    ImGuiHelpers.SetNextWindowPosRelativeMainViewport(new Vector2(position.X + size.X + 7, position.Y + 7), ImGuiCond.Always);

                    ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(7f, 7f));
                    ImGui.PushStyleVar(ImGuiStyleVar.WindowMinSize, new Vector2(200f, 200f));
                    ImGui.Begin($"###WorkshopHelper", ImGuiWindowFlags.NoScrollbar
                        | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.AlwaysUseWindowPadding);

                    DrawWindowContents();

                    ImGui.End();
                    ImGui.PopStyleVar(2);
                }
            }
        }

        private void DrawWindowContents()
        {
            if (ImGui.Button("导入腾讯文档"))
            {
                try
                {
                    MultiCycleList.Clear();

                    var text = ImGui.GetClipboardText();
                    if (StringExtensions.IsNullOrEmpty(text))
                        return;

                    var rawItemStrings = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                    ScheduleImport(rawItemStrings);

                    if (MultiCycleList.All(x => x.PrimarySchedule.Count == 0))
                        PrintPluginMessage("无法分析剪贴板中的任何项目。有关如何导入，请参阅帮助图标。");
                }
                catch (Exception e)
                {
                    PrintPluginMessage("无法分析剪贴板中的任何项目。有关如何导入，请参阅帮助图标。");
                    PluginLog.Error($"无法分析剪贴板。剪贴板可能为空。\n{e}");
                }
            }
            ImGuiComponents.HelpMarker("这是用于从剪贴板中导入腾讯文档作业的生产计划。\n" +
                "该导入器检测每行上的商品名称。\n" +
                "你可以从腾讯文档中复制整个工房的生产计划。");

            ImGui.SameLine();
            if (ImGui.Button("点击打开无人岛作业文档"))
            {
                Util.OpenLink("https://docs.qq.com/doc/DTUNRZkJjTVhvT2Nv");
            }

            ImGui.Text("执行前请务必检查是否识别完整。");

            if (ImGui.RadioButton($"自动选择工房", autoWorkshopSelect))
            {
                autoWorkshopSelect = true;
            }

            ImGuiComponents.HelpMarker("如果剪贴板中商品的生产累计小时数小于24小时，将安排生产计划至所有工房。\n" +
                "如果生产累计小时大于24小时，则将生产计划的前24小时应用于工房1-3号，其余应用于工房4号。");

            if (ImGui.RadioButton($"手动选择工房", !autoWorkshopSelect))
            {
                autoWorkshopSelect = false;
            }

            ImGuiComponents.HelpMarker("用于一次导入一个工房的项目，并允许您选择将生产计划应用于哪些工房。");

            ImGui.Text("导入预览");

            ImGui.BeginChild("ScrollableSection", new Vector2(0, (!autoWorkshopSelect || MultiCycleList.All(x => x.PrimarySchedule.Count == 0) || (MultiCycleList.Count == 1 && MultiCycleList[0].SecondarySchedule.Count == 0) ? 6 : 12) * ImGui.GetTextLineHeightWithSpacing()));

            foreach (var cycle in MultiCycleList)
            {
                if (MultiCycleList.IndexOf(cycle) > 0 && !autoWorkshopSelect)
                    continue;

                var cycleNum = MultiCycleList.IndexOf(cycle)
                    + (selectedCycle == 0 ? MJIManager.Instance()->CurrentCycleDay + nextDayOffset
                    : selectedCycle);

                //var cycleNum = adjustedCycleNums[MultiCycleList.IndexOf(cycle)];

                DrawWorkshopListBox($"第 {cycleNum} 天，工房 {(!autoWorkshopSelect ? string.Join(", ", Workshops.Where(x => x.Value).Select(x => x.Key + 1)) : (cycle.SecondarySchedule.Count > 0 ? "1-3" : "1-4"))}", cycle.PrimarySchedule);

                if (cycle.SecondarySchedule.Count > 0 && autoWorkshopSelect)
                    DrawWorkshopListBox($"第 {cycleNum} 天，工房 4", cycle.SecondarySchedule);
            }
            ImGui.EndChild();

            ImGui.Text("选择日期");

            ImGuiComponents.HelpMarker("保留为空，则在下一个可用的日期执行。");

            ImGui.SetNextItemWidth(100);
            var cyclePrev = selectedCycle == 0 ? "" : Cycles[selectedCycle - 1].ToString();
            if (ImGui.BeginCombo("", cyclePrev))
            {
                if (ImGui.Selectable("", selectedCycle == 0))
                    selectedCycle = 0;

                foreach (var cycle in Cycles)
                {
                    var selected = ImGui.Selectable(cycle.ToString(), selectedCycle == cycle);

                    if (selected)
                        selectedCycle = cycle;
                }
                ImGui.EndCombo();
            }
            ImGui.SameLine();

            var SelectedUnavailable = selectedCycle - 1 == MJIManager.Instance()->CurrentCycleDay || MJIManager.Instance()->CraftworksRestDays[1] <= MJIManager.Instance()->CurrentCycleDay;
            if (SelectedUnavailable)
                ImGui.BeginDisabled();

            if (ImGui.Button("设置休息"))
            {
                TaskManager.Enqueue(() => SetRestDay(selectedCycle));
            }

            if (SelectedUnavailable)
                ImGui.EndDisabled();

            ImGui.SameLine();
            if (ImGui.Button("前一天"))
            {
                OpenCycle(selectedCycle - 1);
                selectedCycle = selectedCycle == 0 ? 0 : selectedCycle - 1;
            }

            ImGui.SameLine();
            if (ImGui.Button("后一天"))
            {
                OpenCycle(selectedCycle + 1);
                selectedCycle = selectedCycle == 14 ? 14 : selectedCycle + 1;
            }

            if (autoWorkshopSelect)
                ImGui.BeginDisabled();

            try
            {
                ImGui.Text("选择工房");

                if (!autoWorkshopSelect)
                {
                    for (var i = 0; i < Workshops.Count; i++)
                    {
                        if (!IsWorkshopUnlocked(i + 1))
                            ImGui.BeginDisabled();

                        try
                        {
                            var configValue = Workshops[i];
                            if (ImGui.Checkbox($"{i + 1}", ref configValue))
                            { Workshops[i] = configValue; }
                            if (i != Workshops.Count - 1)
                                ImGui.SameLine();
                        }
                        catch (Exception ex)
                        {
                            ex.Log();
                        }

                        if (!IsWorkshopUnlocked(i + 1))
                            ImGui.EndDisabled();
                    }

                    ImGui.SameLine();
                }


                if (ImGui.Button("取消选择"))
                    Workshops.Keys.ToList().ForEach(key => Workshops[key] = false);
            }
            catch (Exception ex)
            {
                ex.Log();
            }
            if (autoWorkshopSelect)
                ImGui.EndDisabled();

            try
            {
                var ScheduleContainsRest = MultiCycleList.Any(x => x.PrimarySchedule.Any(y => y.OnRestDay == true));
                if (ScheduleContainsRest)
                {
                    ImGui.TextColored(ImGuiColors.TankBlue, $"{(overrideRest ? "蓝色周期的休息日将被覆盖" : "蓝色周期将被设置成休息")}");
                    ImGui.Checkbox("覆盖休息日？", ref overrideRest);
                }

                var BadFortune = GetCurrentRestDays()[1] != 6 && MultiCycleList.Count == 5;
                if (BadFortune)
                {
                    ImGui.TextColored(ImGuiColors.DalamudRed, "检测到无人岛静态作业导入。");
                    ImGui.Checkbox("是否安排？", ref overrideExecutionDisable);
                    ImGuiComponents.HelpMarker("在使用静态作业时，总是推荐在第一天和第七天时休息。\n" +
                        "作为检查,执行按钮已被禁用，但如果执意要这样安排，请随意勾选确认框。");
                }

                var IsInsufficientRank = (PrimarySchedule.Count > 0 && PrimarySchedule.Any(x => x.InsufficientRank))
                    || (SecondarySchedule.Count > 0 && SecondarySchedule.Any(x => x.InsufficientRank));
                var ScheduleInProgress = selectedCycle - 1 <= MJIManager.Instance()->CurrentCycleDay && selectedCycle != 0;
                var restDays = GetCurrentRestDays();
                var SelectedIsRest = restDays.Contains(selectedCycle - 1);
                var NoWorkshopsSelected = Workshops.Values.All(x => !x) && !autoWorkshopSelect;

                if (IsInsufficientRank || ScheduleInProgress || SelectedIsRest || NoWorkshopsSelected || (BadFortune && !overrideExecutionDisable))
                {
                    if (IsInsufficientRank)
                        ImGui.TextColored(ImGuiColors.DalamudRed, "等级不足，无法执行计划");
                    if (SelectedIsRest)
                        ImGui.TextColored(ImGuiColors.DalamudRed, "所选周期为休息日。\n无法安排休息日。");
                    if (ScheduleInProgress)
                        ImGui.TextColored(ImGuiColors.DalamudRed, "无法在进行中或已过去的日子中执行计划");
                    if (NoWorkshopsSelected)
                        ImGui.TextColored(ImGuiColors.DalamudRed, "未选择工房。\n打开自动选择工房或手动选择工房。");

                    ImGui.BeginDisabled();
                }

                if (ImGui.Button("执行生产计划"))
                {
                    currentWorkshop = Workshops.FirstOrDefault(pair => pair.Value).Key;
                    currentDay = (selectedCycle == 0 ? MJIManager.Instance()->CurrentCycleDay + nextDayOffset
                        : selectedCycle);
                    ScheduleMultiCycleList();
                }

                if (IsInsufficientRank || ScheduleInProgress || SelectedIsRest || NoWorkshopsSelected || (BadFortune && !overrideExecutionDisable))
                    ImGui.EndDisabled();
            }
            catch (Exception e) { PluginLog.Log(e.ToString()); return; }
        }

        private bool IsWorkshopUnlocked(int w)
        {
            try
            {
                var currentRank = MJIManager.Instance()->IslandState.CurrentRank;
                switch (w)
                {
                    case 1:
                        if (currentRank < 3)
                        {
                            maxWorkshops = 0;
                            return false;
                        }
                        break;
                    case 2:
                        if (currentRank < 6)
                        {
                            maxWorkshops = 1;
                            return false;
                        }
                        break;
                    case 3:
                        if (currentRank < 8)
                        {
                            maxWorkshops = 2;
                            return false;
                        }
                        break;
                    case 4:
                        if (currentRank < 14)
                        {
                            maxWorkshops = 3;
                            return false;
                        }
                        break;
                }
                return true;
            }
            catch (Exception ex)
            {
                ex.Log();
                return false;
            }
        }

        private static void DrawWorkshopListBox(string text, List<Item> schedule)
        {
            if (!StringExtensions.IsNullOrEmpty(text))
                ImGui.Text(text);

            ImGui.SetNextItemWidth(250);
            if (ImGui.BeginListBox($"##Listbox{text}", new Vector2(250, (schedule.Count > 0 ? schedule.Count + 1 : 3) * ImGui.GetTextLineHeightWithSpacing())))
            {
                if (schedule.Count > 0)
                {
                    foreach (var item in schedule)
                    {
                        if (item.OnRestDay || item.InsufficientRank)
                        {
                            if (item.OnRestDay && !item.InsufficientRank)
                                ImGui.TextColored(ImGuiColors.TankBlue, item.Name);
                            else if (item.InsufficientRank && !item.OnRestDay)
                                ImGui.TextColored(ImGuiColors.DalamudRed, item.Name);
                            else if (item.OnRestDay && item.InsufficientRank)
                                ImGuiEx.Text(GradientColor.Get(ImGuiColors.DalamudRed, ImGuiColors.TankBlue), item.Name);
                        }
                        else
                            ImGui.Text(item.Name);
                    }
                }
                ImGui.EndListBox();
            }
        }

        internal static void ScheduleImport(List<string> rawItemStrings)
        {
            var rawCycles = SplitCycles(rawItemStrings);

            foreach (var cycle in rawCycles)
            {
                (var items, var excessItems) = ParseItems(cycle);
                if (items == null || items.Count == 0)
                    continue;
                PrimarySchedule = items;
                SecondarySchedule = excessItems;
                MultiCycleList.Add(new CyclePreset { PrimarySchedule = items, SecondarySchedule = excessItems });
            }
        }

        public static List<List<string>> SplitCycles(List<string> rawLines)
        {
            var cycles = new List<List<string>>();
            var currentCycle = new List<string>();
            foreach (var line in rawLines)
            {
                var newcycle = false;
                var linesWithoutNumbers = line;
                var dotIndex = line.IndexOf('.');
                if (dotIndex >= 0 && dotIndex + 1 < line.Length)
                {
                    linesWithoutNumbers = line[(dotIndex + 1)..];
                    newcycle = true;
                }
                var pattern = @"\[[^\]]*\]|， ×[1-3]|，×[1-3]|[1-3]× ";
                linesWithoutNumbers = Regex.Replace(linesWithoutNumbers, pattern, "");
                PluginLog.Log(linesWithoutNumbers);
                if (currentCycle.Count > 0 && newcycle)
                {
                    cycles.Add(currentCycle);
                    currentCycle = [];
                }
                if (currentCycle.Count == 0)
                    currentCycle = [];
                currentCycle.Add(linesWithoutNumbers.Split(new[] { '、' }, StringSplitOptions.RemoveEmptyEntries));

                //if (line.StartsWith("Cycle"))
                //{
                //    if (currentCycle.Count > 0)
                //    {
                //        cycles.Add(currentCycle);
                //        currentCycle = new List<string>();
                //    }
                //    if (currentCycle.Count == 0)
                //        currentCycle = new List<string>();
                //}
                //currentCycle.Add(line);
            }
            if (currentCycle.Count > 0)
            {
                cycles.Add(currentCycle);
            }

            return cycles;
        }

        public static (List<Item>, List<Item>) ParseItems(List<string> itemStrings)
        {
            var items = new List<Item>();
            var excessItems = new List<Item>();
            var hours = 0;
            var isRest = false;
            foreach (var itemString in itemStrings)
            {
                if (itemString.Contains("休息"))
                    isRest = true;

                var matchFound = false;
                foreach (var craftable in Craftables)
                {
                    if (IsMatch(itemString, craftable.Name))
                    {
                        var item = new Item
                        {
                            Key = craftable.Key,
                            Name = Svc.Data.GetExcelSheet<MJICraftworksObject>().GetRow(craftable.Key).Item.Value.Name.RawString,
                            CraftingTime = craftable.CraftingTime,
                            UIIndex = craftable.Key - 1,
                            LevelReq = craftable.LevelReq,
                            OnRestDay = isRest
                        };
                        item.InsufficientRank = !isCraftworkObjectCraftable(item);
                        Svc.Log.Debug(item.ToString());
                        if (hours < 24)
                            items.Add(item);
                        else
                            excessItems.Add(item);

                        hours += craftable.CraftingTime;
                        matchFound = true;
                    }
                }
                if (!matchFound)
                {
                    PluginLog.Debug($"Failed to match string to craftable: {itemString}");
                }
            }

            return (items, excessItems);
        }

        private static bool IsMatch(string x, string y)
        {
            //var pattern = $@"\b{Regex.Escape(y)}\b";
            //return Regex.IsMatch(x, pattern);
            if (x == "腌萝卜")
                x = "腌小萝卜";
            if (x == "洋葱汤")
                x = "开拓工房洋葱汤";
            if (x == "韭葱汤")
                x = "开拓工房韭葱洋葱汤";
            if (x == "煎菜豆")
                x = "开拓工房煎红花菜豆";
            if (x == "甜新薯")
                x = "开拓工房甜新薯";
            return y.Contains(x);
        }

        private static bool isCraftworkObjectCraftable(Item item) => MJIManager.Instance()->IslandState.CurrentRank >= item.LevelReq;

        private static bool isWorkshopOpen() => Svc.GameGui.GetAddonByName("MJICraftSchedule") != IntPtr.Zero;

        private static unsafe bool OpenCycle(int cycle_day)
        {
            var addon = (AtkUnitBase*)Svc.GameGui.GetAddonByName("MJICraftSchedule");
            if (!isWorkshopOpen() || !IsAddonReady(addon))
                return false;

            try
            {
                var workshopPTR = Svc.GameGui.GetAddonByName("MJICraftSchedule");
                if (workshopPTR == IntPtr.Zero)
                    return false;

                var workshopWindow = (AtkUnitBase*)workshopPTR;
                if (workshopWindow == null)
                    return false;

                Callback.Fire(workshopWindow, false, 20, (uint)(cycle_day - 1));

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static unsafe bool OpenAgenda(uint index, int workshop, int prevHours)
        {
            var addon = (AtkUnitBase*)Svc.GameGui.GetAddonByName("MJICraftSchedule");
            if (!isWorkshopOpen() || !IsAddonReady(addon))
                return false;

            try
            {
                var workshopPTR = Svc.GameGui.GetAddonByName("MJICraftSchedule");
                if (workshopPTR == IntPtr.Zero)
                    return false;

                var workshopWindow = (AtkUnitBase*)workshopPTR;
                if (workshopWindow == null)
                    return false;


                Callback.Fire(workshopWindow, false, 17, (uint)(workshop), (uint)(index == 0 ? 0 : prevHours));

                return true;
            }
            catch
            {
                return false;
            }
        }

        private unsafe bool ScheduleItem(Item item)
        {
            var addon = Svc.GameGui.GetAddonByName("MJICraftSchedule");
            if (addon == IntPtr.Zero)
                return false;
            if (!IsAddonReady((AtkUnitBase*)addon))
                return false;

            try
            {
                var schedulerPTR = Svc.GameGui.GetAddonByName("MJICraftScheduleSetting");
                if (schedulerPTR == IntPtr.Zero)
                    return false;
                var schedulerWindow = (AtkUnitBase*)schedulerPTR;
                if (schedulerWindow == null)
                    return false;

                Callback.Fire(schedulerWindow, false, 11, item.UIIndex);
                Callback.Fire(schedulerWindow, false, 13);
                schedulerWindow->Close(true);

                return true;
            }
            catch
            {
                return false;
            }
        }

        private List<int> GetCurrentRestDays()
        {
            var restDays1 = MJIManager.Instance()->CraftworksRestDays[0];
            var restDays2 = MJIManager.Instance()->CraftworksRestDays[1];
            var restDays3 = MJIManager.Instance()->CraftworksRestDays[2];
            var restDays4 = MJIManager.Instance()->CraftworksRestDays[3];
            return [restDays1, restDays2, restDays3, restDays4];
        }

        private bool SetRestDay(int cycle)
        {
            var addon = Svc.GameGui.GetAddonByName("MJICraftSchedule");
            if (addon == IntPtr.Zero)
                return false;
            if (!IsAddonReady((AtkUnitBase*)addon))
                return false;

            try
            {
                // open rest days addon
                Callback.Fire((AtkUnitBase*)addon, false, 12);

                var restDaysPTR = Svc.GameGui.GetAddonByName("MJICraftScheduleMaintenance");
                if (restDaysPTR == IntPtr.Zero)
                    return false;
                var schedulerWindow = (AtkUnitBase*)restDaysPTR;
                if (schedulerWindow == null)
                    return false;

                var restDays = GetCurrentRestDays();
                if (cycle <= 7 && cycle > 0)
                    restDays[1] = cycle - 1;
                else if (cycle > 7)
                    restDays[3] = cycle - 1;
                else if (cycle <= 0)
                    restDays[1] = MJIManager.Instance()->CurrentCycleDay + 1;

                //if (selectedCycle <= 6 && selectedCycle > 0)
                //    restDays[1] = selectedCycle - 1;
                //else if (selectedCycle >= 7)
                //    restDays[3] = selectedCycle - 1;
                //else if (selectedCycle <= 0)
                //    restDays[1] = 1JIManager.Instance()->CurrentCycleDay + 1;

                var restDaysMask = restDays.Sum(n => (int)Math.Pow(2, n));
                Callback.Fire(schedulerWindow, false, 11, (uint)restDaysMask);

                PluginLog.Debug($"Setting Rest Days to {string.Join("", restDays)} => {restDaysMask}");
                TaskManager.Enqueue(() => ConfirmYesNo());

                return true;
            }
            catch
            {
                return false;
            }
        }

        internal static bool ConfirmYesNo()
        {
            var mjiRest = (AtkUnitBase*)Svc.GameGui.GetAddonByName("MJICraftScheduleMaintenance");
            if (mjiRest == null)
                return false;

            if (mjiRest->IsVisible && TryGetAddonByName<AddonSelectYesno>("SelectYesno", out var addon) &&
                addon->AtkUnitBase.IsVisible &&
                addon->YesButton->IsEnabled &&
                addon->AtkUnitBase.UldManager.NodeList[15]->IsVisible)
            {
                new ClickSelectYesNo((IntPtr)addon).Yes();
                return true;
            }

            return false;
        }

        public bool ScheduleList()
        {
            if (isScheduleRest)
            {
                //var currentVal = selectedCycle;
                //TaskManager.EnqueueImmediate(() => selectedCycle = currentDay, $"SetSelectedCycleToCurrentDay");
                //TaskManager.EnqueueImmediate(() => SetRestDay(currentVal), $"SetRest");
                //TaskManager.EnqueueImmediate(() => selectedCycle = currentVal, $"SetSelectedCycleBackToOriginal");
                return true;
            }

            var hours = 0;
            if (autoWorkshopSelect)
            {
                if (SecondarySchedule.Count > 0)
                {
                    for (var i = 0; i < maxWorkshops - 1; i++)
                    {
                        var ws = 0;
                        TaskManager.EnqueueImmediate(() => hours = 0, $"PSSetHours0");
                        foreach (var item in PrimarySchedule)
                        {
                            ws = i;
                            TaskManager.DelayNextImmediate("PSOpenAgendaDelay", PrimarySchedule.IndexOf(item) == 0 ? Config.taskAfterCycleSwitchDelay : Config.taskDelay);
                            TaskManager.EnqueueImmediate(() => OpenAgenda(item.UIIndex, ws, hours), $"PSOpenAgendaW{ws + 1}");
                            TaskManager.DelayNextImmediate("PSScheduleItemDelay", Config.taskDelay);
                            TaskManager.EnqueueImmediate(() => ScheduleItem(item), $"PSScheduleItemW{ws + 1}");
                            TaskManager.EnqueueImmediate(() => hours += item.CraftingTime, $"PSIncrementHoursW{ws + 1}");
                        }
                    }
                    TaskManager.EnqueueImmediate(() => hours = 0, $"SSSetHours0");
                    foreach (var item in SecondarySchedule)
                    {
                        TaskManager.DelayNextImmediate("SSOpenAgendaDelay", SecondarySchedule.IndexOf(item) == 0 ? Config.taskAfterCycleSwitchDelay : Config.taskDelay);
                        TaskManager.EnqueueImmediate(() => OpenAgenda(item.UIIndex, 3, hours), $"SSOpenAgendaW{maxWorkshops}");
                        TaskManager.DelayNextImmediate("SSScheduleItemDelay", Config.taskDelay);
                        TaskManager.EnqueueImmediate(() => ScheduleItem(item), $"SSScheduleW{maxWorkshops}");
                        TaskManager.EnqueueImmediate(() => hours += item.CraftingTime, $"SSIncrementHoursW{maxWorkshops}");
                    }
                }
                else
                {
                    for (var i = 0; i < maxWorkshops; i++)
                    {
                        var ws = 0;
                        TaskManager.EnqueueImmediate(() => hours = 0, $"PSOSetHours0");
                        foreach (var item in PrimarySchedule)
                        {
                            ws = i;
                            TaskManager.DelayNextImmediate("PSOOpenAgendaDelay", PrimarySchedule.IndexOf(item) == 0 ? Config.taskAfterCycleSwitchDelay : Config.taskDelay);
                            TaskManager.EnqueueImmediate(() => OpenAgenda(item.UIIndex, ws, hours), $"PSOOpenAgendaW{ws + 1}");
                            TaskManager.DelayNextImmediate("PSOScheduleItemDelay", Config.taskDelay);
                            TaskManager.EnqueueImmediate(() => ScheduleItem(item), $"PSOScheduleW{ws + 1}");
                            TaskManager.EnqueueImmediate(() => hours += item.CraftingTime, $"PSOIncrementHoursW{ws + 1}");
                        }
                    }
                }
            }
            else
            {
                for (var i = currentWorkshop; i < Workshops.Count; i++)
                {
                    var ws = 0;
                    if (Workshops[i])
                    {
                        TaskManager.EnqueueImmediate(() => hours = 0, $"MSetHours0");
                        foreach (var item in PrimarySchedule)
                        {
                            ws = i;
                            TaskManager.DelayNextImmediate("MOpenAgendaDelay", PrimarySchedule.IndexOf(item) == 0 ? Config.taskAfterCycleSwitchDelay : Config.taskDelay);
                            TaskManager.EnqueueImmediate(() => OpenAgenda(item.UIIndex, ws, hours), $"MOpenAgendaW{ws + 1}");
                            TaskManager.DelayNextImmediate("MScheduleItemDelay", Config.taskDelay);
                            TaskManager.EnqueueImmediate(() => ScheduleItem(item), $"MScheduleW{ws + 1}");
                            TaskManager.EnqueueImmediate(() => hours += item.CraftingTime, $"MIncrementHoursW{ws + 1}");
                            TaskManager.EnqueueImmediate(() => currentWorkshop += 1, $"MIncrementWFromW{ws + 1}");
                        }
                    }
                }
                TaskManager.EnqueueImmediate(() => currentWorkshop = 0, $"MSetWorkshop0");
            }
            return true;
        }

        public void ScheduleMultiCycleList()
        {
            var restCycleIndex = MultiCycleList.FindIndex(x => x.PrimarySchedule.Any(y => y.OnRestDay == true));
            if (restCycleIndex != -1 && !overrideRest)
            {
                // delay when cycle that is open is the one being set to rest?
                TaskManager.Enqueue(() => SetRestDay(currentDay + restCycleIndex), $"MultiCycleSetRestOn{currentDay + restCycleIndex}");
            }

            TaskManager.Enqueue(() =>
            {
                foreach (var cycle in MultiCycleList)
                {
                    if (MultiCycleList.IndexOf(cycle) > 0 && !autoWorkshopSelect)
                        return;

                    TaskManager.Enqueue(() => OpenCycle(currentDay), $"MultiCycleOpenCycleOn{currentDay}");
                    TaskManager.Enqueue(() => PrimarySchedule = cycle.PrimarySchedule, $"MultiCycleSetPrimaryCycleOn{currentDay}");
                    TaskManager.Enqueue(() => SecondarySchedule = cycle.SecondarySchedule, $"MultiCyleSetSecondaryCycleOn{currentDay}");
                    TaskManager.Enqueue(() => { isScheduleRest = !overrideRest && PrimarySchedule[0].OnRestDay; }, $"MultiCycleCheckRestOn{currentDay}");
                    TaskManager.Enqueue(() => ScheduleList(), $"MultiCycleScheduleListOn{currentDay}");
                    TaskManager.Enqueue(() => currentDay += 1, $"MultiCycleScheduleIncrementDayFrom{currentDay}");
                }
            }, "ScheduleMultiCycleForEach");
        }

        private void CheckIfInvalidSchedule(ref SeString message, ref bool isHandled)
        {
            // Unable to set agenda. Insufficient time for handicraft production.
            if (message.ExtractText() == Svc.Data.GetExcelSheet<LogMessage>().First(x => x.RowId == 10146).Text.ExtractText())
            {
                PluginLog.Log("Detected error in scheduling. Aborting current workshop's queue.");
                TaskManager.Abort();
                if (WorkshopsRemaining())
                {
                    TaskManager.Enqueue(() => currentWorkshop += 1);
                    ScheduleList();
                }
            }
        }

        private bool WorkshopsRemaining()
        {
            return Workshops.Skip(currentWorkshop).Any(pair => pair.Value);
        }

        public void PrintPluginMessage(string msg)
        {
            var message = new XivChatEntry
            {
                Message = new SeStringBuilder()
                .AddUiForeground($"[{P.Name}] ", 45)
                .AddUiForeground($"{Name} ", 62)
                .AddText(msg)
                .Build()
            };

            Svc.Chat.Print(message);
        }

        public void PrintPluginMessageError(string msg)
        {
            var message = new XivChatEntry
            {
                Message = new SeStringBuilder()
                .AddUiForeground($"[{P.Name}] ", 45)
                .AddUiForeground($"{Name} ", 62)
                .AddText(msg)
                .Build(),
                Type = XivChatType.ErrorMessage
            };

            Svc.Chat.Print(message);
        }

        public override void Enable()
        {
            Config = LoadConfig<Configs>() ?? new Configs();
            Craftables = Svc.Data.GetExcelSheet<MJICraftworksObject>()
                .Where(x => x.Item.Row > 0)
                .Select(x => (x.RowId, x.Item.GetDifferentLanguage(ClientLanguage.ChineseSimplified).Value.Name.RawString.Trim(), x.CraftingTime, x.LevelReq))
                .ToArray();
            overlay = new Overlays(this);
            Svc.Toasts.ErrorToast += CheckIfInvalidSchedule;
            base.Enable();
        }

        public override void Disable()
        {
            SaveConfig(Config);
            P.Ws.RemoveWindow(overlay);
            Svc.Toasts.ErrorToast -= CheckIfInvalidSchedule;
            base.Disable();
        }
    }
}
