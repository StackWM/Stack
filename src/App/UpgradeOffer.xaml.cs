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

namespace LostTech.Stack {
    using System.Diagnostics;

    /// <summary>
    /// Interaction logic for UpgradeOffer.xaml
    /// </summary>
    partial class UpgradeOffer {
        public UpgradeOffer() {
            InitializeComponent();
        }

        void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e) {
            Process.Start(e.Uri.ToString());
            e.Handled = true;
        }
    }
}
