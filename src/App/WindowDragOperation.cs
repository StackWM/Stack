namespace LostTech.Stack
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    class WindowDragOperation
    {
        public IntPtr Window { get; }
        public Point StartLocation { get; }

        public WindowDragOperation(IntPtr window, Point startLocation)
        {
            this.Window = window;
            this.StartLocation = startLocation;
        }
    }
}
