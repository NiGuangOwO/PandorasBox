using FFXIVClientStructs.FFXIV.Client.Game.Control;
using System.Collections.Generic;

namespace PandorasBox.Features.Commands
{
    public class InteractCommand : CommandFeature
    {
        public override string Name => "与目标交互";
        public override string Command { get; set; } = "/pinteract";

        public override string[] Alias => new string[] { "/pint" };

        public override string Description => "与当前目标互动。";

        protected unsafe override void OnCommand(List<string> args)
        {
            var target = TargetSystem.Instance()->Target;
            if (target != null)
            TargetSystem.Instance()->InteractWithObject(target);
        }
    }
}
