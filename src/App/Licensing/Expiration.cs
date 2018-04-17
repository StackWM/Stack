namespace LostTech.Stack.Licensing
{
    using System;
    using System.DirectoryServices;
    using System.DirectoryServices.ActiveDirectory;
    using System.Security.Authentication;
    using System.Threading.Tasks;
    using System.Windows;

    static class Expiration
    {
        public static readonly DateTimeOffset EnterpriseStoreExpirationDate = new DateTimeOffset(2018, 7, 15, 0, 0, 0, TimeSpan.Zero);
        static readonly bool IsUwp = new DesktopBridge.Helpers().IsRunningAsUwp();

        public static async Task<bool> HasExpired(bool showWarnings = true) {
            bool enterprise = IsDomainUser();
            if (!enterprise)
                return false;

            if (DateTimeOffset.UtcNow > EnterpriseStoreExpirationDate) {
                MessageBox.Show("Enterprise evaluation has expired. Please, update the app.", "Stack Evaluation", MessageBoxButton.OK, MessageBoxImage.Error);
                return true;
            }

            if (showWarnings) {
                string message =
                    "Commercial use will require subscription fee of $14.99 per 6 months (est.).\n\n"
                    + "Until subscription service is available, you can evaluate Stack at your company without paying subscription fee. ";
                if (IsUwp) {
                    message = "Price of Stack WM in Windows Store is the base price for personal use only. "
                              + message
                              + "You will still need to pay personal license price to extend trial past the first month.";
                }
                message +=
                    $"\n\nThis version will expire on {EnterpriseStoreExpirationDate.LocalDateTime.ToShortDateString()}, when it will have to be updated.";
                MessageBox.Show(message, "Enterprise Evaluation", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            return false;
        }

        internal static bool IsDomainUser() {
            try {
                Domain.GetCurrentDomain();
                return true;
            } catch (ActiveDirectoryOperationException e) when (e.HResult == unchecked((int)0x80131500)) {
                return false;
            } catch (ActiveDirectoryObjectNotFoundException) {
                return true;
            } catch (ActiveDirectoryOperationException) {
                return true;
            } catch (DirectoryServicesCOMException) {
                return true;
            } catch (AuthenticationException) {
                return true;
            }
        }
    }
}
