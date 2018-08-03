namespace LostTech.Stack.Settings
{
    using System;
    using LostTech.App.DataBinding;

    public sealed class NotificationSettings : NotifyPropertyChangedBase, ICopyable<NotificationSettings>
    {
        string acceptedTerms = null;
        bool iamInTrayDone = false;
        string whatsNewVersionSeen = null;
        string osVersionUpgrageSuggested = null;
        DateTimeOffset? lastUpgradeOffer = null;

        public string AcceptedTerms
        {
            get => this.acceptedTerms;
            set {
                this.acceptedTerms = value;
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

        public string OsVersionUpgradeSuggested {
            get => this.osVersionUpgrageSuggested;
            set {
                this.osVersionUpgrageSuggested = value;
                this.OnPropertyChanged();
            }
        }

        public DateTimeOffset? LastUpgradeOffer {
            get => this.lastUpgradeOffer;
            set {
                this.lastUpgradeOffer = value;
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
            LastUpgradeOffer = this.LastUpgradeOffer,
            OsVersionUpgradeSuggested = this.OsVersionUpgradeSuggested,
            WhatsNewVersionSeen = this.WhatsNewVersionSeen,
        };
    }
}
