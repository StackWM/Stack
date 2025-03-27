namespace LostTech.Stack.Utils;

using System.ComponentModel;

using global::Windows.Win32.Foundation;

using static global::Windows.Win32.PInvoke;

static class ConsoleWindow {
    public static void Setup() {
        if (AttachConsole(ATTACH_PARENT_PROCESS))
            return;

        var ex = new Win32Exception();
        var error = (WIN32_ERROR)ex.NativeErrorCode;

        if (error is WIN32_ERROR.ERROR_ACCESS_DENIED
                  or WIN32_ERROR.ERROR_INVALID_PARAMETER
                  or WIN32_ERROR.ERROR_INVALID_HANDLE) {
            if (!AllocConsole())
                throw new Win32Exception();
            else
                return;
        }

        throw ex;
    }
}
