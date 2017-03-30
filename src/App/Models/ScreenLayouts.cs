namespace LostTech.Stack.Models
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.Linq;
    using System.Runtime.Serialization;
    using LostTech.App;
    using LostTech.Windows;

    [DataContract]
    public sealed class ScreenLayouts: ICopyable<ScreenLayouts>, INotifyPropertyChanged
    {
        [DataMember]
        public ObservableCollection<MutableKeyValuePair<string, string>> Map { get; }
            = new ObservableCollection<MutableKeyValuePair<string, string>>();

        public event PropertyChangedEventHandler PropertyChanged;

        public ScreenLayouts Copy()
        {
            var result = new ScreenLayouts();
            foreach (var entry in this.Map)
                result.Map.Add(entry);
            return result;
        }

        public string GetPreferredLayout(Screen screen)
        {
            if (screen == null)
                throw new ArgumentNullException(nameof(screen));
            return this.Map.FirstOrDefault(kv => kv.Key == screen.ID)?.Value;
        }

        public int GetPreferredLayoutIndex(Screen screen)
        {
            if (screen == null)
                throw new ArgumentNullException(nameof(screen));
            for(int i = 0; i < this.Map.Count; i++)
                if (this.Map[i].Key == screen.ID)
                    return i;
            return -1;
        }
    }
}
