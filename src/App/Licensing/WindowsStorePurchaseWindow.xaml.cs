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

namespace LostTech.Stack.Licensing {
    /// <summary>
    /// Interaction logic for WindowsStorePurchaseWindow.xaml
    /// </summary>
    public partial class WindowsStorePurchaseWindow : Window {
        public WindowsStorePurchaseWindow() {
            InitializeComponent();
        }

        public string Text {
            get => this.TextBlock.Text;
            set => this.TextBlock.Text = value;
        }
    }
}
