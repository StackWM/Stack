namespace LostTech.Stack.WindowManagement
{
    using System;
    public class WindowNotFoundException: Exception
    {
        private const string DefaultMessage = "Window was not found in the system";

        public WindowNotFoundException() : base(DefaultMessage) { }

        public WindowNotFoundException(string message, Exception innerException):
            base(message, innerException) { }

        public WindowNotFoundException(Exception innerException):
            base(DefaultMessage, innerException: innerException) { }
    }
}
