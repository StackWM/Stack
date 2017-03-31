namespace LostTech.Stack
{
    using LostTech.Stack.Zones;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Windows;

    class WindowDragOperation
    {
        public IntPtr Window { get; }
        public Point StartLocation { get; }
        public Zone CurrentZone { get; set; }
        public IntPtr OriginalActiveWindow { get; set; }
        public bool Activated { get; internal set; }

        public WindowDragOperation(IntPtr window, Point startLocation)
        {
            this.Window = window;
            this.StartLocation = startLocation;
        }
    }
}
