namespace LostTech.Stack.Settings
{
    using System;
    using LostTech.App;
    using LostTech.Stack.DataBinding;

    public sealed class KeyboardMoveBehaviorSettings: NotifyPropertyChangedBase, ICopyable<KeyboardMoveBehaviorSettings>
    {
        bool enabled = true;

        public bool Enabled
        {
            get => this.enabled;
            set {
                this.enabled = value;
                this.OnPropertyChanged();
            }
        }

        public KeyboardMoveBehaviorSettings Copy() => new KeyboardMoveBehaviorSettings {
            Enabled = this.Enabled,
        };
    }
}