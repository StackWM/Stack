namespace LostTech.Windows
{
    using System;

    public class CanNotFindDesktopException: Exception
    {
        public CanNotFindDesktopException() { }
        public CanNotFindDesktopException(Exception reason):base("Can not find desktop", reason) { }
    }
}
