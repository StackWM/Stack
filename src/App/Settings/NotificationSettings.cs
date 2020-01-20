namespace LostTech.Stack.Settings
{
    using LostTech.App.DataBinding;

    public sealed class NotificationSettings : NotifyPropertyChangedBase, ICopyable<NotificationSettings>
    {
        string acceptedTerms = null;
        bool iamInTrayDone = false;
        string whatsNewVersionSeen = null;
        bool enableTelemetry = false;

        public string AcceptedTerms
        {
            get => this.acceptedTerms;
            set {
                this.acceptedTerms = value;
                this.OnPropertyChanged();
            }
        }

        public bool EnableTelemetry {
            get => this.enableTelemetry;
            set {
                this.enableTelemetry = value;
                this.OnPropertyChanged();
            }
        }

        public string WhatsNewVersionSeen {
            get => this.whatsNewVersionSeen;
            set {
                this.whatsNewVersionSeen = value;
                this.OnPropertyChanged();
            }
        }

        public bool IamInTrayDone
        {
            get => this.iamInTrayDone;
            set {
                this.iamInTrayDone = value;
                this.OnPropertyChanged();
            }
        }

        public NotificationSettings Copy() => new NotificationSettings {
            AcceptedTerms = this.AcceptedTerms,
            EnableTelemetry = this.EnableTelemetry,
            IamInTrayDone = this.IamInTrayDone,
            WhatsNewVersionSeen = this.WhatsNewVersionSeen,
        };
    }
}
