﻿namespace LostTech.Stack.ViewModels
{
    using System;
    using System.ComponentModel;
    using System.Runtime.InteropServices;
    using System.Windows.Input;
    using WindowsDesktop;
    using EventHook.Hooks;
    using JetBrains.Annotations;
    using LostTech.Stack.Models;
    using LostTech.Stack.WindowManagement;
    using LostTech.Stack.Windows;
    using Prism.Commands;
    using System.Windows.Media;
    using System.Windows.Interop;
    using System.Windows;
    using System.Windows.Media.Imaging;
    using System.Threading.Tasks;
    using LostTech.Stack.WindowManagement.WinApi;

    public class AppWindowViewModel : SimpleViewModel, IDisposable, IEquatable<AppWindowViewModel>
    {
        static readonly Win32WindowFactory Win32WindowFactory = new Win32WindowFactory();
        ImageSource icon;
        [NotNull]
        public IAppWindow Window { get; }
        readonly WindowHookEx hook = WindowHookExFactory.Instance.GetHook();
        readonly WindowDesktopHook desktopHook;

        public AppWindowViewModel([NotNull] IAppWindow window) {
            this.Window = window ?? throw new ArgumentNullException(nameof(window));
            this.hook.Minimized += this.HookOnMinimizeChanged;
            this.hook.Unminimized += this.HookOnMinimizeChanged;
            this.hook.TextChanged += this.HookOnTextChanged;
            this.CloseCommand = new DelegateCommand(async () => {
                try {
                    await this.Window.Close();
                } catch (WindowNotFoundException) {}
            });
            if (VirtualDesktop.IsPresent) {
                this.desktopHook = new WindowDesktopHook(window);
                this.desktopHook.PropertyChanged += this.DesktopHookPropertyChanged;
            }
        }

        public ICommand CloseCommand { get; }
        public string Title => this.Window.Title;
        public bool IsMinimized => this.Window.IsMinimized;
        public Guid? DesktopID => this.desktopHook?.DesktopID;

        public ImageSource Icon {
            get {
                if (!(this.Window is Win32Window win32Window))
                    return null;

                if (this.icon != null)
                    return this.icon;

                this.UpdateIcon(win32Window);

                return null;
            }
            private set {
                this.icon = value;
                this.OnPropertyChanged();
            }
        }

        async void UpdateIcon(Win32Window win32Window) {
            try {
                await Task.Yield();
                IntPtr hIcon = await win32Window.GetIcon();
                if (hIcon == IntPtr.Zero)
                    return;

                try {
                    this.Icon = Imaging.CreateBitmapSourceFromHIcon(hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                } catch (COMException e) when (HResult.ERROR_INVALID_CURSOR_HANDLE.EqualsCode(e.HResult)) {}
            } catch (WindowNotFoundException) {}
        }

        void HookOnMinimizeChanged(object sender, WindowEventArgs windowEventArgs) {
            if (Win32WindowFactory.Create(windowEventArgs.Handle).Equals(this.Window))
                this.OnPropertyChanged(nameof(this.IsMinimized));
        }

        void HookOnTextChanged(object sender, WindowEventArgs windowEventArgs) {
            if (Win32WindowFactory.Create(windowEventArgs.Handle).Equals(this.Window))
                this.OnPropertyChanged(nameof(this.Title));
        }

        void DesktopHookPropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(this.desktopHook.DesktopID))
                this.OnPropertyChanged(nameof(this.DesktopID));
        }

        public void Dispose() {
            this.hook.Minimized -= this.HookOnMinimizeChanged;
            this.hook.Unminimized -= this.HookOnMinimizeChanged;
            this.hook.TextChanged -= this.HookOnTextChanged;
            this.desktopHook?.Dispose();
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
