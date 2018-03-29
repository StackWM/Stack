namespace LostTech.Stack.Models
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    public interface IObjectWithProblems
    {
        IList<string> Problems { get; }
        event EventHandler<ErrorEventArgs> ProblemOccurred;
    }
}
