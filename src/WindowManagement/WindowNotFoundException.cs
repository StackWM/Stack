namespace LostTech.Stack.WindowManagement
{
    using System;
    public class WindowNotFoundException: Exception
    {
        public WindowNotFoundException(string message, Exception innerException):
            base(message, innerException) { }

        public WindowNotFoundException(Exception innerException):
            base("Window was not found in the system", innerException: innerException) { }
    }
}
