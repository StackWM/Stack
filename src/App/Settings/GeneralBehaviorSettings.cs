namespace LostTech.Stack.Settings
{
    using LostTech.App.DataBinding;
    public sealed class GeneralBehaviorSettings: NotifyPropertyChangedBase, ICopyable<GeneralBehaviorSettings>
    {
        bool suppressSystemMargin = true;

        public bool SuppressSystemMargin {
            get => this.suppressSystemMargin;
            set {
                this.suppressSystemMargin = value;
                this.OnPropertyChanged();
            }
        }

        public GeneralBehaviorSettings Copy() => new GeneralBehaviorSettings {
            SuppressSystemMargin = this.SuppressSystemMargin,
        };
    }
}
