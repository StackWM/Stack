namespace LostTech.Stack.Utils
{
    using System;
    class EventArgs<T>: EventArgs
    {
        public EventArgs(T subject) {
            this.Subject = subject;
        }

        public T Subject { get; }
    }
}
