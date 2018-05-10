namespace LostTech.Stack.Utils
{
    using System;
    public class EventArgs<T>: EventArgs
    {
        public EventArgs(T subject) {
            this.Subject = subject;
        }

        public T Subject { get; }
    }

    static class Args
    {
        public static EventArgs<T> Create<T>(T value) => new EventArgs<T>(value);
    }
}
