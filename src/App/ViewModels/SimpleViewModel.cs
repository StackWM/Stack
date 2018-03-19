namespace LostTech.Stack.ViewModels
{
    using System.ComponentModel;
    using System.Runtime.CompilerServices;
    using JetBrains.Annotations;

    public class SimpleViewModel: INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        [NotifyPropertyChangedInvocator]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void Set<T>(ref T field, T newValue, [CallerMemberName] string propertyName = null) {
            if (object.Equals(field, newValue))
                return;

            field = newValue;
            this.OnPropertyChanged(propertyName);
        }
    }
}
