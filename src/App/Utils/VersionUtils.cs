namespace LostTech.Stack.Utils
{
    using System;
    using global::Windows.ApplicationModel;

    static class VersionUtils
    {
        public static Version ToVersion(this PackageVersion version) =>
            new Version(version.Major, version.Minor, version.Build, version.Revision);
    }
}
