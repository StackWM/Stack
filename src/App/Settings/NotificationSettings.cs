namespace LostTech.Stack.Settings
{
    using LostTech.App.DataBinding;

    public sealed class NotificationSettings : NotifyPropertyChangedBase, ICopyable<NotificationSettings>
    {
        string acceptedTerms = null;
        bool iamInTrayDone = false;
        int? whatsNewVersionSeen = null;

        public string AcceptedTerms
        {
            get => this.acceptedTerms;
            set {
                this.acceptedTerms = value;
                this.OnPropertyChanged();
            }
        }

        public int? WhatsNewVersionSeen {
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
            IamInTrayDone = this.IamInTrayDone,
            WhatsNewVersionSeen = this.WhatsNewVersionSeen,
        };
    }
}
