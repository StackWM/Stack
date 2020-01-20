namespace LostTech.Stack.Settings
{
    using LostTech.App.DataBinding;

    public sealed class NotificationSettings : NotifyPropertyChangedBase, ICopyable<NotificationSettings>
    {
        bool iamInTrayDone = false;

        public bool IamInTrayDone
        {
            get => this.iamInTrayDone;
            set {
                this.iamInTrayDone = value;
                this.OnPropertyChanged();
            }
        }

        public NotificationSettings Copy() => new NotificationSettings {
            IamInTrayDone = this.IamInTrayDone,
        };
    }
}
