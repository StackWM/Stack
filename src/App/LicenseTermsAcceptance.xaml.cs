using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace LostTech.Stack
{
    using System.Reflection;

    /// <summary>
    /// Interaction logic for LicenseTermsAcceptance.xaml
    /// </summary>
    public partial class LicenseTermsAcceptance : Window
    {
        public LicenseTermsAcceptance()
        {
            this.InitializeComponent();

            var @namespace = typeof(LicenseTermsAcceptance).Namespace;
            var resourceName = $"{@namespace}.Terms.html";
            var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
            this.LicenseContent.NavigateToStream(resource);
        }
    }
}
