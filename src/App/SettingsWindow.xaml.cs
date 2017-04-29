namespace LostTech.Stack
{
    using System.ComponentModel;
    using System.Linq;
    /// <summary>
    /// Interaction logic for Settingx.xaml
    /// </summary>
    public partial class SettingsWindow
    {
        public SettingsWindow()
        {
            this.InitializeComponent();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            e.Cancel = true;
            base.OnClosing(e);
            this.Hide();
        }
    }
}
