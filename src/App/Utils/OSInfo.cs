namespace LostTech.Stack.Utils {
    using System;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using global::Windows.ApplicationModel;
    using global::Windows.Management.Deployment;

    public static class OSInfo {
        public static bool SupportsDesktopBridge() {
            var osVer = Environment.OSVersion.Version;
            if (osVer.Major < 10
                || osVer.Major == 10 && osVer.Build < 14393)
                return false;

            return HasStore();
        }

        static bool HasStore() {
            try {
                return HasStoreInternal();
            } catch (Exception e) {
                e.ReportAsWarning("Warning: Can't create PackageManager: ");
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        static bool HasStoreInternal() {
            var packageManager = new PackageManager();
            Package storePackage = packageManager.FindPackagesForUser(
                userSecurityId: string.Empty,
                packageFamilyName: "Microsoft.StorePurchaseApp_8wekyb3d8bbwe").FirstOrDefault();
            return storePackage != null;
        }
    }
}
