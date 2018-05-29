namespace LostTech.Stack.Settings {
    using System.Collections.ObjectModel;
    using System.Windows.Forms;
    using LostTech.App.DataBinding;

    public sealed class MouseMoveBehaviorSettings : NotifyPropertyChangedBase, ICopyable<MouseMoveBehaviorSettings>
    {
        bool enabled = true;
        MouseButtons dragButton = MouseButtons.Middle;

        public bool Enabled {
            get => this.enabled;
            set {
                this.enabled = value;
                this.OnPropertyChanged();
            }
        }

        public MouseButtons DragButton {
            get => this.dragButton;
            set {
                this.dragButton = value;
                this.OnPropertyChanged();
            }
        }

        public ObservableCollection<string> WindowGroupIgnoreList { get; } =
            new ObservableCollection<string>();

        public MouseMoveBehaviorSettings Copy()
        {
            var copy = new MouseMoveBehaviorSettings {
                Enabled = this.Enabled,
                DragButton = this.DragButton,
            };
            foreach (string groupName in this.WindowGroupIgnoreList)
                copy.WindowGroupIgnoreList.Add(groupName);
            return copy;
        }
    }
}