namespace LostTech.Stack.Settings
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows.Input;
    using LostTech.App;
    using LostTech.Stack.DataBinding;

    public sealed class CommandKeyBinding : NotifyPropertyChangedBase, ICopyable<CommandKeyBinding>
    {
        string commandName;
        KeyGesture key;

        public string CommandName {
            get => this.commandName;
            set {
                this.commandName = value;
                this.OnPropertyChanged();
            }
        }

        public KeyGesture Key {
            get => this.key;
            set {
                this.key = value;
                this.OnPropertyChanged();
            }
        }

        public CommandKeyBinding Copy() => new CommandKeyBinding {
            CommandName = this.CommandName,
            Key = this.Key,
        };
    }
}
