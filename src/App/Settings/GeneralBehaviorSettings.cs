namespace LostTech.Stack.Settings
{
    using System.Collections.ObjectModel;
    using LostTech.App.DataBinding;
    public sealed class GeneralBehaviorSettings: NotifyPropertyChangedBase, ICopyable<GeneralBehaviorSettings>
    {
        bool suppressSystemMargin = true;
        bool? captureOnStackStart;
        bool? captureOnDesktopSwitch;
        bool? captureOnAppStart;
        bool? captureOnLayoutChange;

        public bool SuppressSystemMargin {
            get => this.suppressSystemMargin;
            set {
                this.suppressSystemMargin = value;
                this.OnPropertyChanged();
            }
        }

        public bool? CaptureOnStackStart {
            get => this.captureOnStackStart;
            set {
                this.captureOnStackStart = value;
                this.OnPropertyChanged();
            }
        }

        public bool? CaptureOnDesktopSwitch {
            get => this.captureOnDesktopSwitch;
            set {
                this.captureOnDesktopSwitch = value;
                this.OnPropertyChanged();
            }
        }

        public bool? CaptureOnAppStart {
            get => this.captureOnAppStart;
            set {
                this.captureOnAppStart = value;
                this.OnPropertyChanged();
            }
        }

        public bool? CaptureOnLayoutChange {
            get => this.captureOnLayoutChange;
            set {
                this.captureOnLayoutChange = value;
                this.OnPropertyChanged();
            }
        }

        public ObservableCollection<string> CaptureIgnoreList { get; } =
            new ObservableCollection<string>();

        public GeneralBehaviorSettings Copy() {
            var copy = new GeneralBehaviorSettings {
                SuppressSystemMargin = this.SuppressSystemMargin,
                CaptureOnAppStart = this.CaptureOnAppStart,
                CaptureOnDesktopSwitch = this.CaptureOnDesktopSwitch,
                CaptureOnStackStart = this.CaptureOnStackStart,
                CaptureOnLayoutChange = this.CaptureOnLayoutChange,
            };
            foreach(string groupName in this.CaptureIgnoreList)
                copy.CaptureIgnoreList.Add(groupName);
            return copy;
        }
    }
}
