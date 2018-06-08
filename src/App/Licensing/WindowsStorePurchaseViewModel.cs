namespace LostTech.Stack.Licensing {
    using System.Windows.Input;

    class WindowsStorePurchaseViewModel {
        public string Title { get; set; }
        public bool HasTrial { get; set; }
        public string Price { get; set; }
        public ICommand Purchase { get; set; }
    }
}
