namespace LostTech.Stack.Windows
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Drawing;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows.Forms;
    using Gma.System.MouseKeyHook;
    using JetBrains.Annotations;
    using LostTech.Stack.Utils;
    using PInvoke;

    public sealed class DragHook : IDisposable
    {
        public MouseButtons Button { get; private set; }
        public event EventHandler<DragHookEventArgs> DragStartPreview;
        public event EventHandler<DragHookEventArgs> DragStart;
        public event EventHandler<DragHookEventArgs> DragMove;
        public event EventHandler<DragHookEventArgs> DragEnd;

        readonly IMouseEvents hook;
        Point dragStart;
        bool dragging = false;
        int releasing = 0;

        static readonly SortedList<MouseButtons, User32.MOUSEEVENTF> ButtonDownEventCodes = new SortedList<MouseButtons, User32.MOUSEEVENTF> {
            [MouseButtons.Middle] = User32.MOUSEEVENTF.MOUSEEVENTF_MIDDLEDOWN,
            [MouseButtons.Left] = User32.MOUSEEVENTF.MOUSEEVENTF_LEFTDOWN,
            [MouseButtons.Right] = User32.MOUSEEVENTF.MOUSEEVENTF_RIGHTDOWN,
            [MouseButtons.XButton1] = User32.MOUSEEVENTF.MOUSEEVENTF_XDOWN,
            [MouseButtons.XButton2] = User32.MOUSEEVENTF.MOUSEEVENTF_XDOWN,
        };
        static readonly SortedList<MouseButtons, User32.MOUSEEVENTF> ButtonUpEventCodes = new SortedList<MouseButtons, User32.MOUSEEVENTF>
        {
            [MouseButtons.Middle] = User32.MOUSEEVENTF.MOUSEEVENTF_MIDDLEUP,
            [MouseButtons.Left] = User32.MOUSEEVENTF.MOUSEEVENTF_LEFTUP,
            [MouseButtons.Right] = User32.MOUSEEVENTF.MOUSEEVENTF_RIGHTUP,
            [MouseButtons.XButton1] = User32.MOUSEEVENTF.MOUSEEVENTF_XUP,
            [MouseButtons.XButton2] = User32.MOUSEEVENTF.MOUSEEVENTF_XUP,
        };
        static readonly SortedList<MouseButtons, uint> XButton = new SortedList<MouseButtons, uint> {
            [MouseButtons.Middle] = 0,
            [MouseButtons.Left] = 0,
            [MouseButtons.Right] = 0,
            [MouseButtons.XButton1] = MOUSE_XBUTTON1,
            [MouseButtons.XButton2] = MOUSE_XBUTTON2,
        };

        bool isPressed;

        public DragHook(MouseButtons button, [NotNull] IMouseEvents mouseHook)
        {
            if (button == MouseButtons.None || button.BitCount() != 1)
                throw new ArgumentException(
                    $"{nameof(button)} must contain exactly one button",
                    paramName: nameof(button));

            this.Button = button;
            this.hook = mouseHook ?? throw new ArgumentNullException(nameof(mouseHook));

            this.hook.MouseDownExt += this.OnMouseDown;
            this.hook.MouseMoveExt += this.OnMouseMove;
            this.hook.MouseUpExt += this.OnMouseUp;
        }

        void OnMouseUp(object sender, MouseEventExtArgs eventArgs)
        {
            var button = eventArgs.Button & this.Button;
            if (button == MouseButtons.None || !this.isPressed)
                return;

            eventArgs.Handled = true;

            if (!this.dragging) {
                this.ReleaseCapture(eventArgs.Location);
                Debug.WriteLine("drag cancelled");
            }
            else {
                var args = new DragHookEventArgs(eventArgs.X, eventArgs.Y);
                this.DragEnd?.Invoke(this, args);
                eventArgs.Handled = args.Handled;
                this.isPressed = false;
            }

            this.dragging = false;
        }

        void OnMouseMove(object sender, MouseEventExtArgs eventArgs)
        {
            if (!this.isPressed)
                return;

            if (!this.dragging) {
                var dragSize = SystemInformation.DragSize;
                if (Math.Abs(eventArgs.X - this.dragStart.X) <= dragSize.Width
                    && Math.Abs(eventArgs.Y - this.dragStart.Y) <= dragSize.Height)
                    return;

                var args = new DragHookEventArgs(this.dragStart.X, this.dragStart.Y);
                this.DragStart?.Invoke(this, args);
                if (!args.Handled) {
                    this.ReleaseCapture();
                    return;
                }
                this.dragging = true;
            }

            this.DragMove?.Invoke(this, new DragHookEventArgs(eventArgs.X, eventArgs.Y));
        }

        public void ReleaseCapture(Point? upLocation = null, [CallerMemberName] string by = null)
        {
            Task.Factory.StartNew(() => {
                // replay captured event
                var up = upLocation ?? this.dragStart;
                Interlocked.Increment(ref this.releasing);
                SendMouseInput(ButtonDownEventCodes[this.Button], this.dragStart.X, this.dragStart.Y, XButton[this.Button]);
                SendMouseInput(ButtonUpEventCodes[this.Button], up.X, up.Y, XButton[this.Button]);
                Interlocked.Decrement(ref this.releasing);
            });
            this.isPressed = false;
        }

        public void SetButton(MouseButtons button) {
            if (this.Button == button)
                return;

            if (!ButtonDownEventCodes.ContainsKey(button))
                throw new NotSupportedException($"Button {button} is not supported");

            this.ReleaseCapture();
            this.Button = button;
        }

        const uint MOUSE_XBUTTON1 = 1, MOUSE_XBUTTON2 = 2;

        void SendMouseInput(User32.MOUSEEVENTF eventType, int x, int y, uint XBUTTON = 0)
        {
            User32.SendInput(1, new[] {
                    new User32.INPUT {
                        type = User32.InputType.INPUT_MOUSE,
                        Inputs = new User32.INPUT.InputUnion {
                            mi = new User32.MOUSEINPUT {
                                dwFlags = eventType | User32.MOUSEEVENTF.MOUSEEVENTF_ABSOLUTE,
                                dx = x,
                                dy = y,
                                mouseData = XBUTTON,
                            }
                        }
                    }
                }, Marshal.SizeOf<User32.INPUT>());
        }

        void OnMouseDown(object sender, MouseEventExtArgs eventArgs)
        {
            var buttons = eventArgs.Button & this.Button;

            if (buttons == MouseButtons.None || this.isPressed || Volatile.Read(ref this.releasing) != 0)
                return;

            var dragArgs = new DragHookEventArgs(eventArgs.X, eventArgs.Y) {
                // by default we will handle drag
                Handled = true
            };
            this.DragStartPreview?.Invoke(this, dragArgs);
            if (!dragArgs.Handled)
                return;

            this.isPressed = true;
            this.dragStart = eventArgs.Location;
            eventArgs.Handled = true;
        }

        public void Dispose()
        {
            this.ReleaseCapture();

            this.hook.MouseDownExt -= this.OnMouseDown;
            this.hook.MouseMoveExt -= this.OnMouseMove;
            this.hook.MouseUpExt -= this.OnMouseUp;
        }
    }
}
