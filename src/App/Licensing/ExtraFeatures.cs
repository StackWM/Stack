namespace LostTech.Stack.Licensing
{
    using System;
    using System.IO;

    class ExtraFeatures
    {
        public static ErrorEventArgs PaidFeature(string featureName) =>
            new ErrorEventArgs(new NotSupportedException($"Feature {featureName} requires paid version. Get it from Windows Store."));
    }
}
