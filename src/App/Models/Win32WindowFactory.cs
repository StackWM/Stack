namespace LostTech.Stack.Models
{
    using System;

    class Win32WindowFactory
    {
        public bool SuppressSystemMargin { get; set; } = true;
        public Win32Window Create(IntPtr handle) => new Win32Window(handle, this.SuppressSystemMargin);
    }
}
