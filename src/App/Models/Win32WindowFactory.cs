namespace LostTech.Stack.Models
{
    using System;
    using PInvoke;

    class Win32WindowFactory
    {
        public bool SuppressSystemMargin { get; set; } = true;
        public Win32Window Create(IntPtr handle) => new Win32Window(handle, this.SuppressSystemMargin);
        public Win32Window Foreground => this.CreateIfNotNull(User32.GetForegroundWindow());
        public Win32Window Desktop => this.CreateIfNotNull(User32.GetDesktopWindow());
        public Win32Window Shell => this.CreateIfNotNull(User32.GetShellWindow());

        Win32Window CreateIfNotNull(IntPtr handle) =>
            handle == IntPtr.Zero ? null : new Win32Window(handle, this.SuppressSystemMargin);
    }
}
