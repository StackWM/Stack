namespace LostTech.Stack.ViewModels
{
    using System;
    using EventHook.Hooks;
    using JetBrains.Annotations;
    using LostTech.Stack.Models;

    public class AppWindowViewModel : SimpleViewModel, IDisposable, IEquatable<AppWindowViewModel>
    {
        static readonly Win32WindowFactory Win32WindowFactory = new Win32WindowFactory();
        [NotNull]
        public IAppWindow Window { get; }
        readonly WindowHookEx hook = new WindowHookEx();

        public AppWindowViewModel([NotNull] IAppWindow window) {
            this.Window = window ?? throw new ArgumentNullException(nameof(window));
            this.hook.Minimized += this.HookOnMinimizeChanged;
            this.hook.Unminimized += this.HookOnMinimizeChanged;
        }

        public string Title => this.Window.Title;
        public bool IsMinimized => this.Window.IsMinimized;

        void HookOnMinimizeChanged(object sender, WindowEventArgs windowEventArgs) {
            if (Win32WindowFactory.Create(windowEventArgs.Handle).Equals(this.Window))
                this.OnPropertyChanged(nameof(this.IsMinimized));
        }

        public void Dispose() {
            this.hook.Dispose();
        }

        public bool Equals(AppWindowViewModel other) {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(this.Window, other.Window);
        }

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((AppWindowViewModel)obj);
        }

        public override int GetHashCode() => (this.Window != null ? this.Window.GetHashCode() : 0);
    }
}
