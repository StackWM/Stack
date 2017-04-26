namespace LostTech.Stack.Settings
{
    using System.Collections.Generic;
    using System.Linq;
    using LostTech.App;
    using LostTech.Stack.DataBinding;

    public sealed class MouseMoveBehaviorSettings : NotifyPropertyChangedBase, ICopyable<MouseMoveBehaviorSettings>
    {
        bool enabled = true;

        public bool Enabled {
            get => this.enabled;
            set {
                this.enabled = value;
                this.OnPropertyChanged();
            }
        }

        public MouseMoveBehaviorSettings Copy() => new MouseMoveBehaviorSettings {
            Enabled = this.Enabled,
        };
    }
}