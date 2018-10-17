namespace LostTech.Stack.Licensing
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.DirectoryServices;
    using System.DirectoryServices.ActiveDirectory;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Security.Authentication;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Interop;
    using global::Windows.Services.Store;
    using Microsoft.HockeyApp;
    using Prism.Commands;

    static class Expiration
    {
        public static readonly DateTimeOffset EnterpriseStoreExpirationDate = new DateTimeOffset(2018, 7, 15, 0, 0, 0, TimeSpan.Zero);
        static readonly bool IsUwp = new DesktopBridge.Helpers().IsRunningAsUwp();

        public static async Task<bool> HasExpired() {
            bool enterprise = IsDomainUser();
            if (!enterprise)
                return false;

            bool evaluationAllowed = DateTimeOffset.UtcNow <= EnterpriseStoreExpirationDate;

            try {
                if (await HasEnterpriseSubscription())
                    return false;

                string title = "Purchase an enterprise subscription";
                string message = evaluationAllowed
                    ? "You can use Stack WM on your office computer until July 15, 2018 without a subscription for evaluation. \n\nSign up now to ensure uninterrupted experience:"
                    : "Additional subscription is required to use Stack WM for commercial purposes.\n\nPlease, pick a subscription below:";

                if (await PromptUserToPurchase(title, message))
                    return false;

                if (DateTimeOffset.UtcNow > EnterpriseStoreExpirationDate) {
                    MessageBox.Show("You must purchase a subscription. Trial options are available", "Stack Evaluation",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return true;
                }

                return false;
            } catch (Exception ex) {
                HockeyClient.Current.TrackException(ex);
                return !evaluationAllowed;
            }
        }

        static async Task<bool> PromptUserToPurchase(string title, string text) {
            var durables = await LicensingContext.Value.GetAssociatedStoreProductsAsync(new[] {"Durable"});
            if (durables.ExtendedError != null) {
                HockeyClient.Current.TrackException(durables.ExtendedError, new Dictionary<string, string> {
                    ["warning"] = "true",
                });
                return false;
            }

            var products = durables.Products.Values
                .Where(product => product.Skus.Any(sku => sku.CustomDeveloperData?.Split('\n')?.Contains("enterprise") == true))
                .ToArray();

            if (products.Length == 0) {
                HockeyClient.Current.TrackException(new InvalidProgramException("Can't find any enterprise subscription products"));
                MessageBox.Show("We could not find any subscriptions. Incident will be reported.",
                    "Windows Store issue", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            bool success = false;
            WindowsStorePurchaseWindow window = null;
            var options = products.Select(p => new WindowsStorePurchaseViewModel {
                Price = p.Skus.Last().Price.FormattedRecurrencePrice,
                HasTrial = p.Skus.Any(s => s.SubscriptionInfo.HasTrialPeriod),
                Title = p.Title,
                Purchase = new DelegateCommand(async () => {
                    window.IsEnabled = false;
                    try {
                        var result = await p.RequestPurchaseAsync();
                        switch (result.Status) {
                        case StorePurchaseStatus.Succeeded:
                        case StorePurchaseStatus.AlreadyPurchased:
                            success = true;
                            window.DialogResult = true;
                            break;
                        default:
                            Trace.WriteLine(result.Status);
                            Trace.WriteLine(result.ExtendedError?.Message);
                            break;
                        }
                    } finally {
                        window.IsEnabled = true;
                    }
                }),
            }).ToArray();

            window = new WindowsStorePurchaseWindow {
                Title = title,
                Text = text,
                DataContext = options,
            };

            window.ShowDialog();
            window.Close();
            return success;
        }

        static async Task<bool> HasEnterpriseSubscription() {
            var license = await LicensingContext.Value.GetAppLicenseAsync();

            foreach (var addon in license.AddOnLicenses) {
                Trace.WriteLine($"addon: {addon.Value.SkuStoreId} active: {addon.Value.IsActive}");
                var addonLicense = addon.Value;
                if (addonLicense.IsActive)
                    return true;
            }

            return false;
        }

        static readonly Lazy<StoreContext> LicensingContext = new Lazy<StoreContext>(InitializeLicensingContext, isThreadSafe: false);

        static StoreContext InitializeLicensingContext() {
            var context = StoreContext.GetDefault();
            var init = (IInitializeWithWindow)(object)context;
            IntPtr mainHandle = new WindowInteropHelper(App.Current.MainWindow).Handle;
            init.Initialize(mainHandle);
            return context;
        }

        [ComImport]
        [Guid("3E68D4BD-7135-4D10-8018-9FB6D9F33FA1")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IInitializeWithWindow {
            void Initialize(IntPtr hwnd);
        }

        internal static bool IsDomainUser() {
            string domainName = GetDomainName();
            return IsDomainUser(domainName);
        }

        internal static bool IsDomainUser(string domainName) => domainName != null && !domainName.Equals("tntech.edu", StringComparison.InvariantCultureIgnoreCase);

        internal static string GetDomainName() {
            try {
                return Domain.GetCurrentDomain().Name;
            } catch (ActiveDirectoryOperationException e) when (
                e.HResult == unchecked((int)0x80131500)) {
                return null;
            } catch (ActiveDirectoryObjectNotFoundException) {
                return "<err:onf>";
            } catch (ActiveDirectoryOperationException) {
                return "<err:op>";
            } catch (DirectoryServicesCOMException) {
                return "<err:com>";
            } catch (ActiveDirectoryServerDownException) {
                return "<err:down>";
            } catch (AuthenticationException) {
                return "<err:auth>";
            }
        }
    }
}
