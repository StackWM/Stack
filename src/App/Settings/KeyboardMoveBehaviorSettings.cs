namespace LostTech.Stack.Settings
{
    using System;
    using System.Collections.ObjectModel;
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

        public ObservableCollection<string> WindowGroupIgnoreList { get; } =
            new ObservableCollection<string>();

        public KeyboardMoveBehaviorSettings Copy()
        {
            var copy = new KeyboardMoveBehaviorSettings
            {
                Enabled = this.Enabled,
            };
            foreach (string groupName in this.WindowGroupIgnoreList)
                copy.WindowGroupIgnoreList.Add(groupName);
            return copy;
        }
    }
}