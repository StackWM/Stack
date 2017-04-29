namespace LostTech.Stack.Settings
{
    using System.Collections.Generic;
    using System.Linq;
    using LostTech.App;

    public sealed class CopyableCommandKeyBinding : CommandKeyBinding, ICopyable<CopyableCommandKeyBinding>
    {
        public CopyableCommandKeyBinding Copy() => new CopyableCommandKeyBinding {
            CommandName = this.CommandName,
            Shortcut = this.Shortcut,
        };
    }
}
