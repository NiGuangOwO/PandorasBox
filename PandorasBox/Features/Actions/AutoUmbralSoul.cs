using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.JobGauge.Types;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.EzHookManager;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game;
using PandorasBox.FeaturesSetup;
using System;

namespace PandorasBox.Features.Actions
{
    internal class AutoUmbralSoul : Feature
    {
        public override string Name => "自动灵极魂";

        public override string Description => "脱离战斗时自动使用灵极魂。如果处于星极火，它会首先转换为灵极冰。如果不在元素中，它将不会执行任何操作。";

        public override FeatureType FeatureType => FeatureType.Actions;

        public override void Enable()
        {
            TaskManager.ShowDebug = false;
            Svc.Framework.Update += RunFeature;
            if (UseActionHook is null) EzSignatureHelper.Initialize(this);
            else UseActionHook?.Enable();
            Svc.Condition.ConditionChange += DelayOutOfCombat;
            base.Enable();
        }

        private void DelayOutOfCombat(ConditionFlag flag, bool value)
        {
            if (Player.Object is null) return;
            if (Player.Job != Job.BLM) return;
            if (Svc.Condition[ConditionFlag.InCombat]) return;

            if (flag == ConditionFlag.InCombat && !value)
            {
                var gauge = Svc.Gauges.Get<BLMGauge>();
                var delay = Math.Max(0, Math.Min(2000, gauge.ElementTimeRemaining - 1000));
                TaskManager.Abort();
                TaskManager.DelayNextImmediate(delay);
            }
        }

        private void RunFeature(IFramework framework)
        {
            TaskManager.Enqueue(() =>
            {
                if (Player.Object is null) return;
                if (Player.Job != Job.BLM) return;
                if (Svc.Condition[ConditionFlag.InCombat]) return;

                var gauge = Svc.Gauges.Get<BLMGauge>();
                if (gauge.InAstralFire)
                {
                    UseAction(149);
                }
                if (gauge.InUmbralIce && gauge.ElementTimeRemaining != 15000)
                {
                    Svc.Log.Debug("Popping Umbral Soul");
                    UseAction(16506);
                }
            });
        }

        public override unsafe bool UseActionDetour(ActionManager* actionManager, ActionType actionType, uint actionId, ulong targetId, uint extraParam, ActionManager.UseActionMode mode, uint comboRouteId, bool* outOptAreaTargeted)
        {
            var ret = base.UseActionDetour(actionManager, actionType, actionId, targetId, extraParam, mode, comboRouteId, outOptAreaTargeted);
            if (actionId == 149) return ret;
            TaskManager.Abort();
            var castTime = ActionManager.GetAdjustedCastTime(actionType, actionId) + 2000;
            TaskManager.DelayNextImmediate(castTime);
            return ret;
        }

        public override void Disable()
        {
            UseActionHook?.Disable();
            Svc.Condition.ConditionChange -= DelayOutOfCombat;
            Svc.Framework.Update -= RunFeature;
            base.Disable();
        }
    }
}
