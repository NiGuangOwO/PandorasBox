using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Party;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using ECommons;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using PandorasBox.FeaturesSetup;

namespace PandorasBox.Features.UI;

public class AutoVoteMvp : Feature
{
    public override string Name => "自动点赞";
    public override string Description => "副本结束时自动点赞小队中对位的队友。";
    public override FeatureType FeatureType => FeatureType.UI;

    public override bool UseAutoConfig => false;
    private List<string> PremadePartyID { get; set; } = [];
    private List<uint> DeadPlayers { get; set; } = [];
    private Dictionary<uint, int> DeathTracker { get; set; } = [];

    public class Configs : FeatureConfig
    {
        public int Priority = 0;
        public bool HideChat = false;
        public bool ExcludeDeaths = false;
        public int HowManyDeaths = 1;
        public bool ResetOnWipe = false;
    }

    public Configs Config { get; private set; } = null!;

    public override unsafe void Enable()
    {
        if (GameMain.Instance()->CurrentContentFinderConditionId != 0)
        {
            var payload = PandoraPayload.Payloads.ToList();
            payload.Add(new TextPayload(" [自动点赞] 请注意，由于此功能是在副本期间启用的，如果您在加入之前与队伍中的其他玩家一起匹配，则可能无法正常运行。"));
            Svc.Chat.Print(new SeString(payload));
        }
        Config = LoadConfig<Configs>() ?? new Configs();
        Svc.Framework.Update += FrameworkUpdate;
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "BannerMIP", OnBannerSetup);
        Svc.Condition.ConditionChange += UpdatePartyCache;
        base.Enable();
    }

    private void UpdatePartyCache(Dalamud.Game.ClientState.Conditions.ConditionFlag flag, bool value)
    {
        if (Svc.Condition.Any())
        {
            if (flag == Dalamud.Game.ClientState.Conditions.ConditionFlag.WaitingForDuty && value)
            {
                foreach (var partyMember in Svc.Party)
                {
                    Svc.Log.Debug($"Adding {partyMember.Name.GetText()} {partyMember.EntityId} to premade list");
                    PremadePartyID.Add(partyMember.Name.GetText());
                }
            }

            if (flag == Dalamud.Game.ClientState.Conditions.ConditionFlag.BoundByDuty && !value)
            {
                PremadePartyID.Clear();
            }
        }
    }

    public override void Disable()
    {
        SaveConfig(Config);
        Svc.Framework.Update -= FrameworkUpdate;
        Svc.AddonLifecycle.UnregisterListener(OnBannerSetup);
        Svc.Condition.ConditionChange -= UpdatePartyCache;
        base.Disable();
    }

    private unsafe void FrameworkUpdate(IFramework framework)
    {
        if (Player.Object == null) return;
        if (Svc.ClientState.IsPvP) return;
        CheckForDeadPartyMembers();
    }

    private unsafe void OnBannerSetup(AddonEvent type, AddonArgs args)
    {
        if (Svc.ClientState.IsPvP) return;
        var atk = (AtkUnitBase*)args.Addon.Address;
        try
        {
            if (ChoosePlayer(atk) is not -1 and var playerIndex)
                VoteBanner((AtkUnitBase*)args.Addon.Address, playerIndex);
        }
        catch (Exception e)
        {
            Svc.Log.Error(e, "Failed to vote!");
        }
    }

    private void CheckForDeadPartyMembers()
    {
        if (!Svc.Party.Any())
        {
            DeathTracker.Clear();
            DeadPlayers.Clear();
            return;
        }

        if (Config.ResetOnWipe && Svc.Party.All(x => x.GameObject?.IsDead == true))
            DeathTracker.Clear();

        foreach (var pm in Svc.Party.Where(pm => pm.GameObject != null && pm.EntityId != Svc.Objects.LocalPlayer?.GameObjectId))
        {
            if (pm.GameObject?.IsDead ?? false)
            {
                if (DeadPlayers.Contains(pm.EntityId)) continue;
                DeadPlayers.Add(pm.EntityId);
                if (DeathTracker.ContainsKey(pm.EntityId))
                    DeathTracker[pm.EntityId] += 1;
                else
                    DeathTracker.TryAdd(pm.EntityId, 1);
            }
            else
                DeadPlayers.Remove(pm.EntityId);
        }
    }

    private unsafe int ChoosePlayer(AtkUnitBase* bannerWindow)
    {
        var hud = UIModule.Instance()->GetAgentModule()->GetAgentHUD();
        if (hud == null) throw new Exception("HUD is empty!");

        var list = Svc.Party.Where(i => i.EntityId != Svc.Objects.LocalPlayer?.GameObjectId && i.GameObject != null && !PremadePartyID.Any(y => y == i.Name.TextValue))
                .Select(PartyMember => (Math.Max(0, GetPartySlotIndex(PartyMember.EntityId, hud) - 1), PartyMember))
                .ToList();

        if (!list.Any()) return -1;

        if (Config.ExcludeDeaths)
        {
            foreach (var deadPlayers in DeathTracker)
            {
                if (deadPlayers.Value >= Config.HowManyDeaths)
                {
                    list.RemoveAll(x => x.PartyMember.EntityId == deadPlayers.Key);
                }
            }
        }

        var tanks = list.Where(i => i.PartyMember.ClassJob.Value.Role == 1);
        var healer = list.Where(i => i.PartyMember.ClassJob.Value.Role == 4);
        var dps = list.Where(i => i.PartyMember.ClassJob.Value.Role is 2 or 3);
        var melee = list.Where(i => i.PartyMember.ClassJob.Value.Role is 2);
        var range = list.Where(i => i.PartyMember.ClassJob.Value.Role is 3);
        var myjob = Svc.ClientState.LocalPlayer.ClassJob.Value.Role;
        (int index, IPartyMember member) voteTarget = new();
        if (myjob == 1)
        {
            voteTarget = tanks.Any() ? tanks.FirstOrDefault() : healer.FirstOrDefault();
        }
        else if (myjob == 4)
        {
            voteTarget = healer.Any() ? healer.FirstOrDefault() : tanks.FirstOrDefault();
        }
        else if (myjob == 2)
        {
            voteTarget = melee.Any() ? melee.FirstOrDefault() : range.FirstOrDefault();
        }
        else
        {
            voteTarget = range.Any() ? range.FirstOrDefault() : melee.FirstOrDefault();
        }

        if (voteTarget.member == null) return -1;

        for (int i = 22; i <= 22 + 7; i++)
        {
            if (bannerWindow->AtkValues[i].Type != AtkValueType.String) continue;
            var name = bannerWindow->AtkValues[i].String.ToString();
            if (name == voteTarget.member.Name.TextValue)
            {
                if (!Config.HideChat)
                {
                    var payload = PandoraPayload.Payloads.ToList();
                    payload.AddRange(
                    [
                        new TextPayload("点赞给 "),
                        voteTarget.member.ClassJob.Value!.Role switch
                        {
                            1 => new IconPayload(BitmapFontIcon.Tank),
                            4 => new IconPayload(BitmapFontIcon.Healer),
                            _ => new IconPayload(BitmapFontIcon.DPS),
                        },
                        new PlayerPayload(voteTarget.member.Name.TextValue, voteTarget.member.World.Value.RowId),
                    ]);
                    Svc.Chat.Print(new SeString(payload));
                }

                Svc.Log.Debug($"Commed {name} at index {i}, {bannerWindow->AtkValues[i - 14].UInt}");
                return (int)bannerWindow->AtkValues[i - 14].UInt;
            }
        }

        return -1;
    }

    private static unsafe int GetPartySlotIndex(uint GameObjectId, AgentHUD* hud)
    {
        var list = hud->PartyMembers;
        for (var i = 0; i < hud->PartyMemberCount; i++)
        {
            if (list[i].Object->GetGameObjectId() == GameObjectId)
            {
                return i;
            }
        }

        return 0;
    }

    private static T RandomPick<T>(IEnumerable<T> list)
        => list.ElementAt(new Random().Next(list.Count() - 1));

    private static unsafe void VoteBanner(AtkUnitBase* bannerWindow, int index)
    {
        if (index == -1) return;
        var atkValues = stackalloc AtkValue[2];
        atkValues[0].SetInt(12);
        atkValues[1].SetInt(index);
        bannerWindow->FireCallback(2, atkValues);
    }

    protected override DrawConfigDelegate DrawConfigTree => (ref bool _) =>
    {
        bool hasChanged = false;

        if (ImGui.Checkbox("不在聊天中显示点赞结果", ref Config.HideChat))
            hasChanged = true;

        if (ImGui.Checkbox("排除死亡过的队友", ref Config.ExcludeDeaths))
            hasChanged = true;

        if (Config.ExcludeDeaths)
        {
            if (ImGui.DragInt("大于等于多少次", ref Config.HowManyDeaths, 0.01f, 1, 100)) hasChanged = true;
            if (ImGui.Checkbox("团灭时重置死亡次数统计", ref Config.ResetOnWipe)) hasChanged = true;
        }

        if (hasChanged)
            SaveConfig(Config);
    };
}
