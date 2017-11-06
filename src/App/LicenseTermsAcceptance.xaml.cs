namespace LostTech.Stack
{
    using System;
    using System.IO;
    using System.Reflection;
    using System.Security.Cryptography;
    using System.Windows;

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
