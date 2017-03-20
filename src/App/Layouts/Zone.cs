namespace LostTech.Stack.Layouts
{
    using System.Windows;

    class Zone
    {
        public Zone() { }

        public Zone(Rect boundaries) { this.Boundaries = this.DropBoundaries = boundaries; }
        public Rect Boundaries { get; set; }
        public Rect DropBoundaries { get; set; }
    }
}
