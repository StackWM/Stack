using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

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
