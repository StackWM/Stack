namespace LostTech.Windows.Win32
{
    using System.Runtime.InteropServices;

    struct DisplayDevice
    {
        public int Size;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string Name;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string String;
        public DisplayDeviceStateFlags StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string ID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Key;

        public bool IsActive => this.StateFlags.HasFlag(DisplayDeviceStateFlags.AttachedToDesktop);
        public bool IsPrimary => this.StateFlags.HasFlag(DisplayDeviceStateFlags.PrimaryDevice);
        public bool IsValid => this.Size > 0;

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumDisplayDevices(string deviceName, int deviceIndex, ref DisplayDevice device,
            EnumDisplayDevicesFlags flags);

        public enum EnumDisplayDevicesFlags
        {
            None = 0,
        }
    }
}