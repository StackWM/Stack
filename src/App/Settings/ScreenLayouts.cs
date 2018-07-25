namespace LostTech.Stack.Settings
{
    using System;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Runtime.Serialization;
    using JetBrains.Annotations;
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
            string layoutByDesignation = this.Map.FirstOrDefault(kv => kv.Key == designation)?.Value;
            string finalLayout = layoutByDesignation ?? this.Map.FirstOrDefault(kv => kv.Key == screen.ID)?.Value;
            return finalLayout;
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

        public void SetPreferredLayout([NotNull] Win32Screen screen, [NotNull] string fileName) {
            if (screen == null)
                throw new ArgumentNullException(nameof(screen));
            if (string.IsNullOrEmpty(fileName))
                throw new ArgumentNullException(nameof(fileName));
            char[] invalidChars = Path.GetInvalidFileNameChars();
            if (fileName.Any(invalidChars.Contains))
                throw new ArgumentException($"{nameof(fileName)} contains invalid chars", paramName: nameof(fileName));

            string key = GetDesignation(screen);
            int existingEntryIndex = this.GetPreferredLayoutIndex(screen);
            var entry = new MutableKeyValuePair<string, string>(key, fileName);
            if (existingEntryIndex < 0)
                this.Map.Add(entry);
            else
                this.Map[existingEntryIndex] = entry;
        }

        public bool NeedsUpdate(Win32Screen screen) {
            int currentIndex = this.GetPreferredLayoutIndex(screen);
            if (currentIndex < 0)
                return true;
            string key = this.Map[currentIndex].Key;
            return int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out int _);
        }

        public static string GetDesignation(Win32Screen screen) {
            var area = screen.WorkingArea;
            return FormattableString.Invariant(
                $"{(int)area.Width}x{(int)area.Height} @ {(int)area.Left},{(int)area.Top}");
        }
    }
}
