namespace LostTech.Stack.Utils
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Windows;
    using System.Windows.Interop;

    static class GlassEffect
    {
        public static void TryEnableGlassEffect(this Window window) {
            try {
                SetGlassEffectImpl(window, true);
            } catch { }
        }

        public static void TryDisableGlassEffect(this Window window) {
            try {
                SetGlassEffectImpl(window, false);
            } catch { }
        }

        static void SetGlassEffectImpl(Window window, bool enable) {
            var windowHelper = new WindowInteropHelper(window);

            var accent = new AccentPolicy();
            int accentStructSize = Marshal.SizeOf(accent);
            accent.AccentState = enable ? AccentState.ACCENT_ENABLE_BLURBEHIND : AccentState.ACCENT_DISABLED;

            IntPtr accentPtr = Marshal.AllocHGlobal(accentStructSize);
            try {
                Marshal.StructureToPtr(accent, accentPtr, fDeleteOld: false);

                var data = new WindowCompositionAttributeData {
                    Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY,
                    SizeOfData = accentStructSize,
                    Data = accentPtr
                };

                SetWindowCompositionAttribute(windowHelper.Handle, ref data);
            } finally {
                Marshal.FreeHGlobal(accentPtr);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct WindowCompositionAttributeData
        {
            public WindowCompositionAttribute Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }

        internal enum WindowCompositionAttribute
        {
            // ...
            WCA_ACCENT_POLICY = 19
            // ...
        }

        internal enum AccentState
        {
            ACCENT_DISABLED = 0,
            ACCENT_ENABLE_GRADIENT = 1,
            ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
            ACCENT_ENABLE_BLURBEHIND = 3,
            ACCENT_INVALID_STATE = 4
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct AccentPolicy
        {
            public AccentState AccentState;
            public int AccentFlags;
            public int GradientColor;
            public int AnimationId;
        }

        [DllImport("user32.dll")]
        internal static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);
    }
}
