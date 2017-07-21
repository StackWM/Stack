namespace LostTech.Stack.Models.Legacy.Filters
{
    using System;
    using System.Diagnostics;
    using LostTech.App.DataBinding;
    using PInvoke;
    using CopyableExtensions = LostTech.App.DataBinding.CopyableExtensions;
    using NotifyPropertyChangedBase = LostTech.App.DataBinding.NotifyPropertyChangedBase;

    public sealed class WindowFilter : NotifyPropertyChangedBase, IFilter<IntPtr>, ICopyable<WindowFilter>
    {
        CommonStringMatchFilter classFilter = new CommonStringMatchFilter();
        CommonStringMatchFilter titleFilter = new CommonStringMatchFilter();

        public bool Matches(IntPtr windowHandle)
        {
            try
            {
                if (!string.IsNullOrEmpty(this.ClassFilter?.Value))
                {
                    string className = User32.GetClassName(windowHandle, maxLength: 4096);
                    if (!this.ClassFilter.Matches(className))
                        return false;
                }

                if (!string.IsNullOrEmpty(this.TitleFilter?.Value))
                {
                    string title = User32.GetWindowText(windowHandle);
                    if (!this.TitleFilter.Matches(title))
                        return false;
                }
            }
            catch (Win32Exception e)
            {
                Debug.WriteLine($"Can't obtain window class or title: {e}");
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
