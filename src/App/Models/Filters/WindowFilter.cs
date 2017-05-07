namespace LostTech.Stack.Models.Filters
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using LostTech.App;
    using LostTech.Stack.DataBinding;
    using PInvoke;

    public sealed class WindowFilter : NotifyPropertyChangedBase, IFilter<IntPtr>, ICopyable<WindowFilter>
    {
        CommonStringMatchFilter classFilter = new CommonStringMatchFilter();
        CommonStringMatchFilter titleFilter = new CommonStringMatchFilter();

        public bool Matches(IntPtr windowHandle)
        {
            if (!string.IsNullOrEmpty(this.ClassFilter?.Value)) {
                string className = User32.GetClassName(windowHandle, maxLength: 4096);
                if (!this.ClassFilter.Matches(className))
                    return false;
            }

            if (!string.IsNullOrEmpty(this.TitleFilter?.Value)) {
                string title = User32.GetWindowText(windowHandle);
                if (!this.TitleFilter.Matches(title))
                    return false;
            }

            return true;
        }

        public CommonStringMatchFilter ClassFilter {
            get => this.classFilter;
            set {
                if (Equals(value, this.classFilter))
                    return;
                this.classFilter = value;
                this.OnPropertyChanged();
            }
        }
        public CommonStringMatchFilter TitleFilter {
            get => this.titleFilter;
            set {
                if (Equals(value, this.titleFilter))
                    return;
                this.titleFilter = value;
                this.OnPropertyChanged();
            }
        }

        public WindowFilter Copy() => new WindowFilter {
            ClassFilter = CopyableExtensions.Copy(this.ClassFilter),
            TitleFilter = CopyableExtensions.Copy(this.TitleFilter),
        };
    }
}
