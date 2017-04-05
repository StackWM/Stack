namespace LostTech.Stack
{
    using LostTech.Stack.Zones;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    class WindowDragOperation
    {
        public IntPtr Window { get; }
        public Zone CurrentZone { get; set; }
        public IntPtr OriginalActiveWindow { get; set; }
        public bool Activated { get; internal set; }

        public WindowDragOperation(IntPtr window)
        {
            this.Window = window;
        }
    }
}
