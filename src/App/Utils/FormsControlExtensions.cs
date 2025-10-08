namespace LostTech.Stack.Utils;

using System;
using System.Windows.Forms;

static class FormsControlExtensions {
    public static void Fire(this Control control, Action action) {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(action);

        if (control.InvokeRequired)
            control.BeginInvoke(action);
        else
            action();
    }
}
