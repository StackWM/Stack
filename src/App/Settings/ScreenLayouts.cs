namespace LostTech.Stack.Settings
{
    using System;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.Linq;
    using System.Runtime.Serialization;
    using System.Windows;
    using LostTech.App.DataBinding;
    using LostTech.Stack.Models;
    using LostTech.Windows;

    [DataContract]
    public sealed class ScreenLayouts: ICopyable<ScreenLayouts>, INotifyPropertyChanged
    {
        [DataMember]
        public ObservableCollection<MutableKeyValuePair<string, string>> Map { get; }
            = new ObservableCollection<MutableKeyValuePair<string, string>>();

#pragma warning disable 0067
        // required for nested tracking
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore 0067

        public ScreenLayouts Copy()
        {
            var result = new ScreenLayouts();
            foreach (var entry in this.Map)
                result.Map.Add(entry);
            return result;
        }

        public string GetPreferredLayout(Win32Screen screen)
        {
            if (screen == null)
                throw new ArgumentNullException(nameof(screen));
            string designation = GetDesignation(screen);
            return this.Map.FirstOrDefault(kv => kv.Key == designation)?.Value
                ?? this.Map.FirstOrDefault(kv => kv.Key == screen.ID)?.Value;
        }

        public int GetPreferredLayoutIndex(Win32Screen screen)
        {
            if (screen == null)
                throw new ArgumentNullException(nameof(screen));
            string designation = GetDesignation(screen);
            for(int i = 0; i < this.Map.Count; i++)
                if (designation == this.Map[i].Key || this.Map[i].Key == screen.ID)
                    return i;
            return -1;
        }

        public static string GetDesignation(Win32Screen screen) {
            Rect area = screen.WorkingArea;
            return FormattableString.Invariant(
                $"{(int)area.Width}x{(int)area.Height} @ {(int)area.Left},{(int)area.Top}");
        }
    }
}
