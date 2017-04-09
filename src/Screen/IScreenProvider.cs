namespace LostTech.Windows
{
    using System.Collections.ObjectModel;

    public interface IScreenProvider
    {
        ReadOnlyObservableCollection<Win32Screen> Screens { get; }
    }
}