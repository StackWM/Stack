namespace LostTech.Stack {
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Windows;
    using System.Windows.Threading;
    using global::Windows.ApplicationModel;
    using global::Windows.Management.Deployment;
    using global::Windows.System;
    using LostTech.Stack.Licensing;
    using LostTech.Stack.Settings;
    using LostTech.Stack.Utils;
    using Microsoft.HockeyApp;
    using static System.FormattableString;
    using static LostTech.Stack.Utils.FileUtils;

    class MigrateToStoreVersion: IDisposable
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

        readonly Dictionary<string, string> telemetryProperties = new Dictionary<string, string> {
            ["correlationID"] = Guid.NewGuid().ToString(),
            [nameof(Expiration.IsDomainUser)] = Invariant($"{Expiration.IsDomainUser()}"),
            [nameof(Version)] = Invariant($"{App.Version}"),
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

            if (!OSInfo.SupportsDesktopBridge()) {
                notifications.LastUpgradeOffer = DateTimeOffset.Now;
                notifications.OsVersionUpgradeSuggested = osVersion;
                HockeyClient.Current.TrackEvent("OS does not support desktop bridge Store apps",
                    properties: new Dictionary<string, string> {
                        ["OSVersion"] = osVersion,
                    });
                return;
            }

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
                HockeyClient.Current.TrackEvent("UpgradeDeferred", this.telemetryProperties);
            } else if (upgrade == null) {
                HockeyClient.Current.TrackEvent("UpgradeOfferIgnored");
            }
            if (upgrade != true)
                return;

            HockeyClient.Current.TrackEvent("UpgradeAccepted", this.telemetryProperties);

            if (!await Launcher.LaunchUriAsync(new Uri("ms-windows-store://pdp/?ProductId=9P4RJ8RL7QGS"))) {
                HockeyClient.Current.TrackException(new NotSupportedException("Could not start Windows Store."), this.telemetryProperties);
                return;
            }

            MessageBox.Show(
                messageBoxText: "Click OK, once you've installed Stack from Windows Store",
                caption: "Waiting for installation to complete",
                button: MessageBoxButton.OK,
                icon: MessageBoxImage.Asterisk);

            if (MessageBox.Show(messageBoxText: "Do you want to migrate layouts and settings to the new version of Stack?",
                    caption: "Migrate settings?",
                    button: MessageBoxButton.YesNo,
                    icon: MessageBoxImage.Question) != System.Windows.MessageBoxResult.Yes) {
                HockeyClient.Current.TrackEvent("StoreMigrationDeclined", this.telemetryProperties);
                return;
            }

            try {
                Migrate();
            } catch (Exception e) {
                HockeyClient.Current.TrackException(e);

                var openHelp = MessageBox.Show(
                    messageBoxText: $"Migration failed due to the following error:\n\n{e}\n\nOpen Q&A article on manual migration?",
                    caption: "Automatic migration failed",
                    button: MessageBoxButton.YesNo,
                    icon: MessageBoxImage.Error);

                if (openHelp == MessageBoxResult.Yes)
                    Process.Start("https://www.allanswered.com/post/qwzpz/manually-migrating-stack-free-settings/");
            }
        }

        public static void Migrate() {
            try {
                Migrate(overwrite: false);
                HockeyClient.Current.TrackEvent("MigratedNoConflict");
            } catch (IOException e) when (e.HResult == IOResult.FileAlreadyExists) {
                if (MessageBox.Show(
                    messageBoxText: "Store version has already been configured! Do you want to overwrite its configuration?",
                    caption: "Overwrite configuration?",
                    button: MessageBoxButton.YesNo,
                    icon: MessageBoxImage.Warning) == MessageBoxResult.Yes) {

                    Migrate(overwrite: true);
                    HockeyClient.Current.TrackEvent("MigratedWithConflict");
                } else {
                    HockeyClient.Current.TrackEvent("StoreMigrationConflictOverwriteDeclined");
                }
            } catch (DirectoryNotFoundException) {
                MessageBox.Show(
                    messageBoxText: "Store version does not seem to be installed.\n"
                                    + "An option to migrate will appear in tray menu next time you start Stack, if store version is installed.",
                    caption: "Store version not found",
                    button: MessageBoxButton.OK,
                    icon: MessageBoxImage.Error);

                HockeyClient.Current.TrackEvent("StoreVersionNotFoundDuringMigration");
            }
        }

        public static void Migrate(bool overwrite) {
            string storeStateRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Packages", App.StoreFamilyName);

            if (!Directory.Exists(storeStateRoot))
                throw new DirectoryNotFoundException();

            string storeLocalState = Path.Combine(storeStateRoot, "LocalState");
            Directory.CreateDirectory(storeLocalState);
            CopyFiles(from: App.AppData.FullName,
                      to: storeLocalState,
                      overwrite: overwrite);
            string storeRoamingState = Path.Combine(storeStateRoot, "RoamingState");
            Directory.CreateDirectory(storeRoamingState);
            CopyFiles(from: App.RoamingAppData.FullName,
                      to: storeRoamingState,
                      overwrite: overwrite);
        }

        public static bool IsStoreVersionInstalled() {
            var packageManager = new PackageManager();
            Package storePackage = packageManager.FindPackagesForUser(
                userSecurityId: string.Empty,
                packageFamilyName: App.StoreFamilyName).FirstOrDefault();
            return storePackage != null;
        }

        public void Dispose() => this.upgradeOfferTimer.Stop();
    }
}
