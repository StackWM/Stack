namespace LostTech.Stack
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Reflection;
    using System.Security.Cryptography;
    using System.Windows;
    using global::Windows.System;
    using LostTech.App;

    /// <summary>
    /// Interaction logic for LicenseTermsAcceptance.xaml
    /// </summary>
    public partial class LicenseTermsAcceptance : Window
    {
        public LicenseTermsAcceptance()
        {
            this.InitializeComponent();

            Stream resource = GetTermsAndConditions();
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
                        BoilerplateApp.Boilerplate.Launch(args.Uri);
                };
            };
        }

        public bool DialogResultSet { get; private set; }

        static Stream GetTermsAndConditions()
        {
            string @namespace = typeof(LicenseTermsAcceptance).Namespace;
            string resourceName = new DesktopBridge.Helpers().IsRunningAsUwp() ? "StoreTerms" : "Terms";
            resourceName = $"{@namespace}.{resourceName}.html";
            return Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
        }

        public static string GetTermsAndConditionsVersion()
        {
            var algorithm = new SHA256CryptoServiceProvider();
            byte[] hash = algorithm.ComputeHash(GetTermsAndConditions());
            return Convert.ToBase64String(hash);
        }

        void AcceptClick(object sender, RoutedEventArgs e) {
            this.DialogResultSet = e.Handled = true;
            this.DialogResult = true;
        }

        void DeclineClick(object sender, RoutedEventArgs e) {
            this.DialogResultSet = e.Handled = true;
            this.DialogResult = false;
        }
    }
}
