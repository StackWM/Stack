namespace LostTech.Stack.DataBinding
{
    using System.ComponentModel;
    using System.Runtime.CompilerServices;

    public class NotifyPropertyChangedBase : INotifyPropertyChanged
    {
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
