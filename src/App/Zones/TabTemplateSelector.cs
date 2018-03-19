namespace LostTech.Stack.Zones
{
    using System.Windows;
    using System.Windows.Controls;
    using LostTech.Stack.Models;
    using LostTech.Stack.ViewModels;

    public class TabTemplateSelector : DataTemplateSelector
    {
        public DataTemplate WindowTemplate { get; set; }
        public DataTemplate ZoneTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container) {
            switch (item) {
            case IAppWindow _:
                return this.WindowTemplate;
            case ZoneViewModel _:
                return this.ZoneTemplate;
            default:
                return null;
            }
        }
    }
}