namespace LostTech.Stack.Settings
{
    using LostTech.App.DataBinding;
    public sealed class GeneralBehaviorSettings: NotifyPropertyChangedBase, ICopyable<GeneralBehaviorSettings>
    {
        bool suppressSystemMargin = true;
        bool captureOnStackStart;
        bool captureOnDesktopSwitch;
        bool captureOnAppStart;

        public bool SuppressSystemMargin {
            get => this.suppressSystemMargin;
            set {
                this.suppressSystemMargin = value;
                this.OnPropertyChanged();
            }
        }

        public bool CaptureOnStackStart {
            get => this.captureOnStackStart;
            set {
                this.captureOnStackStart = value;
                this.OnPropertyChanged();
            }
        }

        public bool CaptureOnDesktopSwitch {
            get => this.captureOnDesktopSwitch;
            set {
                this.captureOnDesktopSwitch = value;
                this.OnPropertyChanged();
            }
        }

        public bool CaptureOnAppStart {
            get => this.captureOnAppStart;
            set {
                this.captureOnAppStart = value;
                this.OnPropertyChanged();
            }
        }

        public GeneralBehaviorSettings Copy() => new GeneralBehaviorSettings {
            SuppressSystemMargin = this.SuppressSystemMargin,
            CaptureOnAppStart = this.CaptureOnAppStart,
            CaptureOnDesktopSwitch = this.CaptureOnDesktopSwitch,
            CaptureOnStackStart = this.CaptureOnStackStart,
        };
    }
}
