namespace LostTech.Stack {
    using System;
    using System.IO;
    using System.Linq;
    using System.Windows;
    using System.Windows.Threading;
    using global::Windows.ApplicationModel;
    using global::Windows.Management.Deployment;
    using global::Windows.System;
    using LostTech.Stack.Settings;
    using LostTech.Stack.Utils;
    using static LostTech.Stack.Utils.FileUtils;

    class MigrateToStoreVersion
    {
        readonly NotificationSettings notificationSettings;

        readonly DispatcherTimer upgradeOfferTimer = new DispatcherTimer {
            Interval =
#if DEBUG
                TimeSpan.FromSeconds(15),
#else
                TimeSpan.FromHours(1),
#endif
        };

        public MigrateToStoreVersion(NotificationSettings notificationSettings) {
            this.notificationSettings = notificationSettings
                ?? throw new ArgumentNullException(nameof(notificationSettings));
        }
        public void SuggestUpgrade() {
            string osVersion = Environment.OSVersion.Version.ToString();
            var notifications = this.notificationSettings;
            if (notifications.LastUpgradeOffer?.AddMonths(3) > DateTimeOffset.Now
                && notifications.OsVersionUpgradeSuggested == osVersion)
                return;

            if (!OSInfo.SupportsDesktopBridge())
                return;

            this.upgradeOfferTimer.Tick += delegate {
                this.SuggestUpgradeNow();
                this.upgradeOfferTimer.Stop();
            };
            this.upgradeOfferTimer.Start();
        }

        async void SuggestUpgradeNow() {
            var upgradeOffer = new UpgradeOffer();
            bool? upgrade = upgradeOffer.ShowDialog();
            if (upgrade == false) {
                this.notificationSettings.LastUpgradeOffer = DateTimeOffset.Now;
                this.notificationSettings.OsVersionUpgradeSuggested = Environment.OSVersion.Version.ToString();
            }
            if (upgradeOffer.ShowDialog() != true)
                return;

            if (!await Launcher.LaunchUriAsync(new Uri("ms-windows-store://pdp/?ProductId=9P4RJ8RL7QGS")))
                return;

            MessageBox.Show(
                messageBoxText: "Click OK, once you've installed Stack from Windows Store",
                caption: "Waiting for installation to complete",
                button: MessageBoxButton.OK,
                icon: MessageBoxImage.Asterisk);

            if (MessageBox.Show(messageBoxText: "Do you want to migrate layouts and settings to the new version of Stack?",
                    caption: "Migrate settings?",
                    button: MessageBoxButton.YesNo,
                    icon: MessageBoxImage.Question) != System.Windows.MessageBoxResult.Yes)
                return;

            Migrate();
        }

        public static void Migrate() {
            try {
                Migrate(overwrite: false);
            } catch (IOException e) when (e.HResult == IOResult.FileAlreadyExists) {
                if (MessageBox.Show(messageBoxText: "Store version has already been configured! Do you want to overwrite its configuration?",
                        caption: "Overwrite configuration?",
                        button: MessageBoxButton.YesNo,
                        icon: MessageBoxImage.Warning) == MessageBoxResult.Yes)
                    Migrate(overwrite: true);
            }
        }

        public static void Migrate(bool overwrite) {
            string storeStateRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Packages", App.StoreFamilyName);
            CopyFiles(from: App.AppData.FullName,
                      to: Path.Combine(storeStateRoot, "LocalState"),
                      overwrite: overwrite);
            CopyFiles(from: App.RoamingAppData.FullName,
                      to: Path.Combine(storeStateRoot, "RoamingState"),
                      overwrite: overwrite);
        }

        public static bool IsStoreVersionInstalled() {
            var packageManager = new PackageManager();
            Package storePackage = packageManager.FindPackagesForUser(
                userSecurityId: string.Empty,
                packageFamilyName: App.StoreFamilyName).FirstOrDefault();
            return storePackage != null;
        }
    }
}
