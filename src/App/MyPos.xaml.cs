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
    /// <summary>
    /// Interaction logic for MyPos.xaml
    /// </summary>
    public partial class MyPos : Window
    {
        public MyPos()
        {
            InitializeComponent();
        }

        private void Window_LocationChanged(object sender, EventArgs e)
        {
            this.PosLeft.Text = "" + this.Left;
            this.PosTop.DataContext = "" + this.Top;
        }
    }
}
