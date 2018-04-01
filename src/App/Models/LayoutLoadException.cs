namespace LostTech.Stack.Models
{
    using System;
    using JetBrains.Annotations;

    public class LayoutLoadException : Exception
    {
        public LayoutLoadException([NotNull] string fileName, [NotNull] string message)
        : base($"{fileName}: {message}") {
            this.FileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
            if (message == null)
                throw new ArgumentNullException(nameof(message));
        }

        public string FileName { get; set; }
    }
}
