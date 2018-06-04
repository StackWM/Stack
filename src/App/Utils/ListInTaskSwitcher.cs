namespace LostTech.Stack.Utils
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Windows;
    using System.Windows.Interop;
    using static PInvoke.User32;
    using Win32Exception = System.ComponentModel.Win32Exception;

    static class ListInTaskSwitcher
    {
        public static bool IsListedInTaskSwitcher(this Window window) {
            var helper = new WindowInteropHelper(window);
            var style = (WindowStylesEx)GetWindowLong(helper.Handle, WindowLongIndexFlags.GWL_EXSTYLE);
            return style.HasFlag(WindowStylesEx.WS_EX_TOOLWINDOW);
        }

        public static Exception SetIsListedInTaskSwitcher(this Window window, bool list) {
            var helper = new WindowInteropHelper(window);
            var style = (WindowStylesEx)GetWindowLong(helper.Handle, WindowLongIndexFlags.GWL_EXSTYLE);
            style = list
                ? style & ~WindowStylesEx.WS_EX_TOOLWINDOW
                : style | WindowStylesEx.WS_EX_TOOLWINDOW;
            return SetWindowLong(helper.Handle, WindowLongIndexFlags.GWL_EXSTYLE,
                       (SetWindowLongFlags)style) == 0
                ? new Win32Exception() : null;
        }
    }
}
