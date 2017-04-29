namespace LostTech.Stack.Settings
{
    using System.Collections.Generic;
    using System.Linq;
    using LostTech.App;

    public static class CopyableCommandKeyBinding
    {
        public static CommandKeyBinding Copy(this CommandKeyBinding binding) => new CommandKeyBinding {
            CommandName = binding.CommandName,
            Shortcut = binding.Shortcut,
        };
    }
}
