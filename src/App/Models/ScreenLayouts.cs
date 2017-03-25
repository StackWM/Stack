namespace LostTech.Stack.Models
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Runtime.Serialization;
    using LostTech.Windows;

    [DataContract]
    public sealed class ScreenLayouts
    {
        [DataMember]
        public ObservableCollection<KeyValuePair<string, string>> Map { get; }
            = new ObservableCollection<KeyValuePair<string, string>>();

        public string GetPreferredLayout(Screen screen)
        {
            if (screen == null)
                throw new ArgumentNullException(nameof(screen));
            return this.Map.FirstOrDefault(kv => kv.Key == screen.ID).Value;
        }
    }
}
