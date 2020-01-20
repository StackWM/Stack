namespace LostTech.Stack
{
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using LostTech.App;

    /// <summary>
    /// Interaction logic for About.xaml
    /// </summary>
    public partial class About
    {
        public About()
        {
            this.InitializeComponent();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            e.Cancel = true;
            this.Hide();
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            BoilerplateApp.Boilerplate.Launch(e.Uri);
            e.Handled = true;
        }
    }
}
