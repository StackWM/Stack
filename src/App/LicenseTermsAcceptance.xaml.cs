namespace LostTech.Stack
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Reflection;
    using System.Security.Cryptography;
    using System.Windows;
    using global::Windows.System;

    /// <summary>
    /// Interaction logic for LicenseTermsAcceptance.xaml
    /// </summary>
    public partial class LicenseTermsAcceptance : Window
    {
        public LicenseTermsAcceptance()
        {
            this.InitializeComponent();

            Stream resource = GetTermsAndCondtions();
            this.LicenseContent.NavigateToStream(resource);
            this.LicenseContent.Navigated += delegate {
                this.LicenseContent.Navigating += (_, args) => {
                    args.Cancel = true;
                    if (!"http".Equals(args.Uri.Scheme, StringComparison.InvariantCultureIgnoreCase)
                        && !"https".Equals(args.Uri.Scheme, StringComparison.InvariantCultureIgnoreCase))
                        return;
                    if (new DesktopBridge.Helpers().IsRunningAsUwp())
                        Launcher.LaunchUriAsync(args.Uri).GetAwaiter();
                    else
                        Process.Start(args.Uri.AbsoluteUri);
                };
            };
        }

        static Stream GetTermsAndCondtions()
        {
            string @namespace = typeof(LicenseTermsAcceptance).Namespace;
            string resourceName = new DesktopBridge.Helpers().IsRunningAsUwp() ? "StoreTerms" : "Terms";
            resourceName = $"{@namespace}.{resourceName}.html";
            return Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
        }

        public static string GetTermsAndConditionsVersion()
        {
            var algorithm = new SHA256CryptoServiceProvider();
            byte[] hash = algorithm.ComputeHash(GetTermsAndCondtions());
            return Convert.ToBase64String(hash);
        }

        void AcceptClick(object sender, RoutedEventArgs e) {
            e.Handled = true;
            this.DialogResult = true;
        }

        void DeclineClick(object sender, RoutedEventArgs e) {
            e.Handled = true;
            this.DialogResult = false;
        }
    }
}
