using ECommons.Automation;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Collections.Generic;

namespace PandorasBox.Features.Commands
{
    internal class LeaveDuty : CommandFeature
    {
        public override string Name => "离开副本";
        public override string Command { get; set; } = "/pdfleave";

        public override string Description => "快速离开副本。";
        protected unsafe override void OnCommand(List<string> args)
        {
            if (GameMain.Instance()->CurrentContentFinderConditionId != 0 && !Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.InCombat])
            {
                Chat.Instance.SendMessage("/dfinder");
                if (Svc.GameGui.GetAddonByName("ContentsFinderMenu") != IntPtr.Zero)
                {
                    var ui = (AtkUnitBase*)Svc.GameGui.GetAddonByName("ContentsFinderMenu");
                    Callback.Fire(ui, true, 0);
                    Callback.Fire(ui, false, -2);

                    var yesno = (AtkUnitBase*)Svc.GameGui.GetAddonByName("SelectYesno");
                    Callback.Fire(yesno, true, 0);
                }
            }
            else
            {
                if (GameMain.Instance()->CurrentContentFinderConditionId == 0)
                {
                    Svc.Chat.PrintError("你没有副本可以离开。");
                    return;
                }

                if (Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.InCombat])
                {
                    Svc.Chat.PrintError("战斗期间不能离开。");
                    return;
                }
            }
        }
    }
}
