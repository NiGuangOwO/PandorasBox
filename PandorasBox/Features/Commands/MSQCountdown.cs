using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.GeneratedSheets;
using System.Collections.Generic;
using System.Linq;

namespace PandorasBox.Features.Commands
{
    public unsafe class MSQCountdown : CommandFeature
    {
        public override string Name => "主线任务倒计数";

        public override string Description => "在聊天中打印一条消息，说明当前大版本中还有多少主线任务需要完成。";

        public override string Command { get; set; } = "/pmsq";

        private ExVersion CurrentExpansion;

        protected override void OnCommand(List<string> args)
        {
            string debug = "";
            if (args.Count > 0)
            {
                debug = args[0];
            }

            var questsheet = Svc.Data.GetExcelSheet<Quest>();
            var uim = UIState.Instance();

            var filteredList = questsheet.Where(x => x.JournalGenre.Value.Icon == 61412 && !string.IsNullOrEmpty(x.Name.RawString));
            CurrentExpansion = Svc.Data.GetExcelSheet<ExVersion>().GetRow(0);

            if (debug == "")
            {
                foreach (var quest in filteredList)
                {
                    if (uim->IsUnlockLinkUnlockedOrQuestCompleted(quest.RowId, quest.ToDoCompleteSeq.Max()))
                    {
                        if (quest.Expansion.Value.RowId > CurrentExpansion.RowId)
                            CurrentExpansion = quest.Expansion.Value;
                    }
                }
            }
            else
            {
                CurrentExpansion = debug.ToLower() switch
                {
                    "arr" => Svc.Data.GetExcelSheet<ExVersion>().GetRow(0),
                    "hw" => Svc.Data.GetExcelSheet<ExVersion>().GetRow(1),
                    "stb" => Svc.Data.GetExcelSheet<ExVersion>().GetRow(2),
                    "shb" => Svc.Data.GetExcelSheet<ExVersion>().GetRow(3),
                    "ew" => Svc.Data.GetExcelSheet<ExVersion>().GetRow(4)
                };
            }

            int completed = 0;
            var totalMSQ = filteredList.Where(x => x.Expansion.Row == CurrentExpansion.RowId).Count();

            int uncompleted = 0;
            foreach (var quest in filteredList.Where(x => x.Expansion.Row == CurrentExpansion.RowId).OrderBy(x => x.Name.RawString))
            {
                if (uim->IsUnlockLinkUnlockedOrQuestCompleted(quest.RowId, quest.ToDoCompleteSeq.Max()))
                {
                    Svc.Log.Debug($"{quest.Name} - Completed!");
                    completed++;
                }
                else
                {
                    Svc.Log.Error($"{quest.Name} - Not Completed!");
                    uncompleted++;
                }

            }

            Svc.Log.Error($"{uncompleted} quests not done, total MSQ is {totalMSQ}.");
            if (CurrentExpansion.RowId == 0)
            {
                if (PlayerState.Instance()->StartTown != 1)
                    totalMSQ -= 23;

                if (PlayerState.Instance()->StartTown != 2)
                    totalMSQ -= 23;

                if (PlayerState.Instance()->StartTown != 3)
                    totalMSQ -= 24;

                totalMSQ -= 8;
            }

            var diff = totalMSQ - completed;

            Svc.Log.Debug($"{diff} - {totalMSQ} {completed}");
            if (diff > 0)
            {
                if (Svc.Data.GetExcelSheet<ExVersion>().Max(x => x.RowId) == CurrentExpansion.RowId)
                {
                    Svc.Chat.Print($"你当前在 {CurrentExpansion.Name} 还剩 {diff} 任务待完成。");
                }
                else
                {
                    Svc.Chat.Print($"你当前在 {CurrentExpansion.Name} 还剩 {diff} 任务到 {Svc.Data.GetExcelSheet<ExVersion>().GetRow(CurrentExpansion.RowId + 1).Name}");
                }
            }

            if (diff == 0)
            {
                if (Svc.Data.GetExcelSheet<ExVersion>().Max(x => x.RowId) == CurrentExpansion.RowId)
                {
                    Svc.Chat.Print($"恭喜你，没有要完成的主线任务了...至少现在！");
                }
                else
                {
                    Svc.Chat.Print($"恭喜你完成 {CurrentExpansion.Name}！继续肝 {Svc.Data.GetExcelSheet<ExVersion>().GetRow(CurrentExpansion.RowId + 1).Name}吧！！！");
                }
            }

            if (diff < 0)
            {
                Svc.Chat.PrintError($"出现错误，你看起来还有 {diff} 个任务？确定不是，请联系开发人员。");
            }
        }
    }
}
