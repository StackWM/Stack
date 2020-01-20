namespace LostTech.Stack {
    using System;
    using System.Collections.Generic;
    using System.Windows;
    using System.Windows.Input;

    /// <summary>
    /// Interaction logic for InputBox.xaml
    /// </summary>
    public partial class InputBox : Window {
        public InputBox() {
            this.InitializeComponent();
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e) {
            if (e.Key == Key.Escape) {
                e.Handled = true;
                this.Close();
                return;
            }

            base.OnPreviewKeyDown(e);
        }

        private void OK_Click(object sender, RoutedEventArgs e) {
            this.DialogResult = true;
            this.Close();
        }
    }
}
