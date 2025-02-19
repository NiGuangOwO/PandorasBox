using Dalamud.Game.Text.SeStringHandling;
using ECommons;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Lumina.Excel.Sheets;
using PandorasBox.FeaturesSetup;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PandorasBox.Features.Commands
{
    public unsafe class QuickPlateLink : CommandFeature
    {
        public override string Name => "投影模板关联";
        public override string Command { get; set; } = "/pglamlink";
        public override string[] Alias => new string[] { "/pgl" };

        public override List<string> Parameters => new() { "[-r <职能>]", "[-j <职业>]", "[-g <套装名字/套装编号>]", "[-n 投影模板编号]" };
        public override string Description => "快速将投影模板与多个套装预设关联。";

        public override FeatureType FeatureType => FeatureType.Commands;

        public struct Gearset
        {
            public byte ID { get; set; }
            public int Slot { get; set; }
            public string Name { get; set; }
            public uint ClassJob { get; set; }
            public byte GlamPlate { get; set; }
        }

        private readonly List<Gearset> gearsets = new();
        private readonly List<string> roles = new() { "坦克", "治疗", "输出", "远程", "读条职业", "远程魔法", "近战", "远程物理", "生产", "能工巧匠", "采集", "大地使者" };
        private List<ClassJob> jobsList = new();
        protected override void OnCommand(List<string> args)
        {
            gearsets.Clear();

            var sortedArgs = new Dictionary<string, List<string>>();

            for(var i = 0; i < args.Count; i++)
            {
                var currentArg = args[i];

                if (currentArg.StartsWith("-"))
                {
                    var flag = currentArg;
                    var values = new List<string>();

                    if (i + 1 < args.Count && !args[i + 1].StartsWith("-"))
                    {
                        for (var j = i + 1; j < args.Count && !args[j].StartsWith("-"); j++)
                        {
                            values.Add(args[j]);
                            i = j;
                        }
                    }

                    sortedArgs[flag] = values;
                }
            }

            if (sortedArgs.ContainsKey("-h"))
            {
                PrintModuleMessage("用法: /pgl -j <职业名称> <投影模板编号>\n/pgl -r <职能> <投影模板编号>");
                return;
            }

            if (sortedArgs.TryGetValue("-j", out var plateValues) && plateValues.Count == 0)
            {
                PrintModuleMessage("无效的投影模板编号");
                return;
            }

            if (sortedArgs.TryGetValue("-j", out var jobValues) && jobValues.Count == 0)
            {
                PrintModuleMessage("无效的职业名称");
                return;
            }

            if (sortedArgs.TryGetValue("-r", out var roleValues) && roleValues.Count == 0)
            {
                PrintModuleMessage($"无效的职能名称\n有效的职能名称： {string.Join(", ", roles)}");
                return;
            }

            var plate = byte.Parse(sortedArgs["-n"][0]);
            foreach (var kvp in sortedArgs)
            {
                var flag = kvp.Key;
                var values = kvp.Value;
                switch (flag)
                {
                    case "-r":
                        if (IsRoleMatch(string.Join(" ", values), out var role))
                        {
                            switch (role)
                            {
                                case "坦克":
                                    jobsList = Svc.Data.GetExcelSheet<ClassJob>().Where(x => x.Role.EqualsAny<byte>(1)).ToList();
                                    break;
                                case "治疗":
                                    jobsList = Svc.Data.GetExcelSheet<ClassJob>().Where(x => x.Role.EqualsAny<byte>(4)).ToList();
                                    break;
                                case "输出":
                                    jobsList = Svc.Data.GetExcelSheet<ClassJob>().Where(x => x.Role.EqualsAny<byte>(2, 3)).ToList();
                                    break;
                                case "近战":
                                    jobsList = Svc.Data.GetExcelSheet<ClassJob>().Where(x => x.Role.EqualsAny<byte>(2)).ToList();
                                    break;
                                case "远程":
                                    jobsList = Svc.Data.GetExcelSheet<ClassJob>().Where(x => (x.UIPriority / 10).EqualsAny<int>(3, 4)).ToList();
                                    break;
                                case "远程魔法":
                                case "读条职业":
                                    jobsList = Svc.Data.GetExcelSheet<ClassJob>().Where(x => (x.UIPriority / 10).Equals(4)).ToList();
                                    break;
                                case "远程物理":
                                    jobsList = Svc.Data.GetExcelSheet<ClassJob>().Where(x => (x.UIPriority / 10).Equals(3)).ToList();
                                    break;
                                case "制作":
                                case "能工巧匠":
                                    jobsList = Svc.Data.GetExcelSheet<ClassJob>().Where(x => (x.UIPriority / 10).Equals(10)).ToList();
                                    break;
                                case "采集":
                                case "大地使者":
                                    jobsList = Svc.Data.GetExcelSheet<ClassJob>().Where(x => (x.UIPriority / 10).Equals(20)).ToList();
                                    break;
                            }
                            ParseGearset(jobsList.Select(job => job.Name.ToString()).ToList(), plate);
                            LinkPlateToGearset(plate);
                        }
                        else
                        {
                            PrintModuleMessage($"作为参数传递的职能名称无效。\n职能： {string.Join(", ", roles)}");
                        }
                        break;
                    case "-g":
                    case "-j":
                        ParseGearset(values, plate);
                        if (gearsets == null || gearsets.Count == 0)
                            PrintModuleMessage($"无法将 {string.Join(" ", values)} 与任何套装预设相匹配");
                        LinkPlateToGearset(plate);
                        break;
                }
            }             
        }

        private void ParseGearset(List<string> args, byte plate)
        {
            var gearsetModule = RaptureGearsetModule.Instance();

            foreach (var arg in args)
            {
                for (var i = 0; i < 100; i++)
                {
                    var gs = gearsetModule->GetGearset(i);
                    if (gs == null || !gs->Flags.HasFlag(RaptureGearsetModule.GearsetFlag.Exists) || gs->Id != i)
                        continue;

                    var name = gs->NameString;
                    var jobAbbrMatch = Svc.Data.GetExcelSheet<ClassJob>().Where(x => x.Abbreviation.ToString().Equals(arg, StringComparison.CurrentCultureIgnoreCase)).ToList();
                    
                    if (arg.Equals(name, StringComparison.CurrentCultureIgnoreCase)
                        || (jobAbbrMatch.Count > 0 && jobAbbrMatch[0].RowId.Equals(gs->ClassJob))
                        || arg.Equals(gs->Id.ToString(), StringComparison.CurrentCultureIgnoreCase)
                        || arg.Equals(gs->ClassJob.ToString(), StringComparison.CurrentCultureIgnoreCase))
                    {
                        gearsets.Add(new Gearset
                        {
                            ID = gs->Id,
                            Slot = i + 1,
                            ClassJob = gs->ClassJob,
                            Name = name,
                            GlamPlate = plate,
                        });
                    }
                }
            }
        }

        private void LinkPlateToGearset(byte plate)
        {
            var gearsetModule = RaptureGearsetModule.Instance();

            foreach (var gs in gearsets)
            {
                gearsetModule->LinkGlamourPlate(gs.ID, gs.GlamPlate);
                var msg = new SeStringBuilder()
                    .AddText("Changed gearset ")
                    .AddUiForeground($"{gs.Name} ", 576)
                    .AddText("to use plate ")
                    .AddUiForeground($"{gs.GlamPlate}", 576)
                    .Build();
                PrintModuleMessage(msg);
            }
        }

        private bool IsRoleMatch(string input, out string matchedRole)
        {
            var inputSplit = input.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            matchedRole = roles.FirstOrDefault(role =>
            {
                var rolesSplit = role.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                return inputSplit.All(x => rolesSplit.Any(roleWord => roleWord.Contains(x)));
            });

            return matchedRole != null;
        }
    }
}
