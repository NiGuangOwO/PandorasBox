using ECommons.DalamudServices;
using ECommons.EzHookManager;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Shell;
using PandorasBox.FeaturesSetup;

namespace PandorasBox.Features.Other
{
    internal class SkipMount : Feature
    {
        public override string Name => "宏中已骑乘时跳过 '/mount'";

        public override string Description => "在宏中使用 /mount 时，如果已骑乘坐骑则跳过该行";

        public override FeatureType FeatureType => FeatureType.Other;

        public override void Enable()
        {
            if (UseActionHook is null)
                EzSignatureHelper.Initialize(this);
            else
                UseActionHook?.Enable();
            base.Enable();
        }

        public override unsafe bool UseActionDetour(ActionManager* actionManager, ActionType actionType, uint actionId, ulong targetId, uint extraParam, ActionManager.UseActionMode mode, uint comboRouteId, bool* outOptAreaTargeted)
        {
            var macroLine = RaptureShellModule.Instance()->MacroCurrentLine;
            if (macroLine > 0)
            {
                var line = RaptureShellModule.Instance()->MacroLineText;
                if (line.ToString().StartsWith("/mount") && Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.Mounted])
                    return false;
            }
            return base.UseActionDetour(actionManager, actionType, actionId, targetId, extraParam, mode, comboRouteId, outOptAreaTargeted);
        }

        public override void Disable()
        {
            UseActionHook?.Disable();
            base.Disable();
        }
    }
}
