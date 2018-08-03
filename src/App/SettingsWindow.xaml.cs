namespace LostTech.Stack
{
    using System;
    using System.ComponentModel;
    using System.Linq;
    using System.Threading.Tasks;

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

        async void CreateGroupClick(object sender, EventArgs e) {
            this.WindowGroupsTab.IsSelected = true;
            // let tab change to be processed
            await Task.Yield();
            this.WindowGroupsEditor.CreateGroup();
        }
    }
}
