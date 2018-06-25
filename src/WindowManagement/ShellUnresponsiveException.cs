namespace LostTech.Stack.WindowManagement
{
    using System;
    public class ShellUnresponsiveException: Exception
    {
        const string DefaultMessage = "Shell is unresponsive";

        public ShellUnresponsiveException() : base(DefaultMessage) { }
        public ShellUnresponsiveException(string message, Exception innerException)
            : base(message, innerException) { }
        public ShellUnresponsiveException(Exception innerException)
            : base(DefaultMessage, innerException: innerException) { }
    }
}
