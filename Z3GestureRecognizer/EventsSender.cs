using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;

namespace PreposeGestureRecognizer
{
    public enum GestureEventType
    {
        NONE,
        MOUSE_LEFT_CLICK, MOUSE_MIDDLE_CLICK, MOUSE_RIGHT_CLICK,
        MOUSE_X_ABSOLUTE, MOUSE_Y_ABSOLUTE, MOUSE_X_MOVE, MOUSE_Y_MOVE, MOUSE_WHEEL, 
        SHIFT, CTRL, ALT, BACKSPACE, CAPSLOCK, DELETE, ENTER, ESC, TAB, UP, DOWN, LEFT, RIGHT, HOME, END, PGUP, PGDN,
        F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,
        a, b, c, d, e, f, g, h, i, j, k, l, m, n, o, p, q, r, s, t, u, v, y, x, w, z
    };

    public static class MouseEventsHelper
    {
        public enum MouseFlag
        {
            MOVE = 0x0001,/* mouse move */
            LEFTDOWN = 0x0002, /* left button down */
            LEFTUP = 0x0004, /* left button up */
            RIGHTDOWN = 0x0008, /* right button down */
            RIGHTUP = 0x0010, /* right button up */
            MIDDLEDOWN = 0x0020, /* middle button down */
            MIDDLEUP = 0x0040, /* middle button up */
            XDOWN = 0x0080, /* x button down */
            XUP = 0x0100, /* x button down */
            WHEEL = 0x0800, /* wheel button rolled */
            HWHEEL = 0x01000, /* hwheel button rolled */
            MOVE_NOCOALESCE = 0x2000, /* do not coalesce mouse moves */
            VIRTUALDESK = 0x4000, /* map to entire virtual desktop */
            ABSOLUTE = 0x8000 /* absolute move */
        }

        const int INPUT_MOUSE = 0;

        struct INPUT
        {
            public int type;
            public InputUnion u;
        }

        [StructLayout(LayoutKind.Explicit)]
        struct InputUnion
        {
            [FieldOffset(0)]
            public MOUSEINPUT mi;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;

            public static implicit operator Point(POINT point)
            {
                return new Point(point.X, point.Y);
            }
        }

        [DllImport("user32.dll")]
        static extern IntPtr GetMessageExtraInfo();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetCursorPos(out POINT lpPoint);

        public static Point GetCursorPosition()
        {
            POINT lpPoint;
            GetCursorPos(out lpPoint);
            //bool success = User32.GetCursorPos(out lpPoint);
            // if (!success)

            return lpPoint;
        }

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        static public void SendMouseInput(MOUSEINPUT mouse)
        {
            INPUT[] inputs = new INPUT[]
            {
                new INPUT
                {
                    type = INPUT_MOUSE,
                    u = new InputUnion
                    {
                        mi = mouse,
                    }
                }
            };
            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        internal static void SendMouseClickEvent(GestureEventType mouseEvent)
        {
            var mouse = new MOUSEINPUT();
            switch (mouseEvent)
            {
                case GestureEventType.MOUSE_LEFT_CLICK:
                    mouse.dwFlags = (uint)MouseFlag.LEFTDOWN;
                    SendMouseInput(mouse);
                    mouse.dwFlags = (uint)MouseFlag.LEFTUP;                    
                    SendMouseInput(mouse);
                    break;
                case GestureEventType.MOUSE_MIDDLE_CLICK:
                    mouse.dwFlags = (uint)MouseFlag.MIDDLEDOWN;
                    SendMouseInput(mouse);
                    mouse.dwFlags = (uint)MouseFlag.MIDDLEUP;                    
                    SendMouseInput(mouse);
                    break;
                case GestureEventType.MOUSE_RIGHT_CLICK:
                    mouse.dwFlags = (uint)MouseFlag.RIGHTDOWN;
                    SendMouseInput(mouse);
                    mouse.dwFlags = (uint)MouseFlag.RIGHTUP;                    
                    SendMouseInput(mouse);
                    break;
            }
        }

        internal static void AddMouseRollEvent(GestureEventType mouseEvent, double percentage, ref MOUSEINPUT mouse)
        {
            var velocity = 0.05;
            var sign = 1;
            var middleDistance = (percentage - 0.5);
            if (middleDistance < 0) sign = -1;
            var middleDistanceSquared = middleDistance * middleDistance * sign;
            switch (mouseEvent)
            {
                case GestureEventType.MOUSE_X_ABSOLUTE:
                    var absoluteX = Math.Min((int)(percentage * 65535.0), 65535);
                    mouse.dx = absoluteX;
                    mouse.dwFlags |= (uint)MouseFlag.MOVE | (uint)MouseFlag.ABSOLUTE;
                    break;
                case GestureEventType.MOUSE_Y_ABSOLUTE:
                    var absoluteY = Math.Min((int)(percentage * 65535.0), 65535);
                    mouse.dy = absoluteY;
                    mouse.dwFlags |= (uint)MouseFlag.MOVE | (uint)MouseFlag.ABSOLUTE;
                    break;
                case GestureEventType.MOUSE_WHEEL:
                    // wheel data can be negative or positive
                    // on this implementation it varies from -100 to 100
                    var wheelMotion = (int)((percentage * 2 - 1.0) * 100);
                    mouse.mouseData = (uint)wheelMotion;
                    mouse.dwFlags |= (uint)MouseFlag.WHEEL;
                    break;
                case GestureEventType.MOUSE_X_MOVE:
                    // middleDistance * middleDistance guarantees a stable behavior around the middle
                    var deltaX = (middleDistanceSquared * 65535.0) * velocity;
                    mouse.dx = Math.Max(0, Math.Min(65535, (int)(mouse.dx + deltaX)));
                    mouse.dwFlags |= (uint)MouseFlag.MOVE | (uint)MouseFlag.ABSOLUTE;
                    break;
                case GestureEventType.MOUSE_Y_MOVE:
                    // middleDistance * middleDistance guarantees a stable behavior around the middle
                    var deltaY = (middleDistanceSquared * 65535.0) * velocity;
                    mouse.dy = Math.Max(0, Math.Min(65535, (int)(mouse.dy + deltaY)));
                    mouse.dwFlags |= (uint)MouseFlag.MOVE | (uint)MouseFlag.ABSOLUTE;
                    break;
            }
        }

        private static void GetCursorRelativePosition(out double x, out double y, double range = 1.0)
        {
            var width = Screen.PrimaryScreen.Bounds.Width;
            var height = Screen.PrimaryScreen.Bounds.Height;
            x = GetCursorPosition().X / (width - 1.0); // current x
            y = GetCursorPosition().Y / (height - 1.0); // current y
            x *= range;
            y *= range;
        }

        internal static MouseEventsHelper.MOUSEINPUT GetCurrentMouseInput()
        {
            var result = new MOUSEINPUT();

            double x;
            double y;
            var win32CursorRange = 65535.0;
            GetCursorRelativePosition(out x, out y, win32CursorRange);

            result.dx = (int)x;
            result.dy = (int)y;

            return result;
        }
    }

    public class GestureEvent
    {
        public GestureEvent()
        {
            EventPart1 = GestureEventType.NONE;
            EventPart2 = GestureEventType.NONE;
            TriggerGestureName = "anything";
        }

        public GestureEvent(string part1String, string part2String, string trigger)
        {
            EventPart1 = (GestureEventType)Enum.Parse(typeof(GestureEventType), part1String);
            EventPart2 = (GestureEventType)Enum.Parse(typeof(GestureEventType), part2String);
            TriggerGestureName = trigger;
        }

        public string MakeKeyboardCode(GestureEventType gestureEventType)
        {
            var result = "";

            switch (gestureEventType)
            {
                case GestureEventType.NONE:
                    break;
                case GestureEventType.SHIFT:
                    result += "+"; break;
                case GestureEventType.CTRL:
                    result += "^"; break;
                case GestureEventType.ALT:
                    result += "%"; break;
                case GestureEventType.a:
                case GestureEventType.b:
                case GestureEventType.c:
                case GestureEventType.d:
                case GestureEventType.e:
                case GestureEventType.f:
                case GestureEventType.g:
                case GestureEventType.h:
                case GestureEventType.i:
                case GestureEventType.j:
                case GestureEventType.k:
                case GestureEventType.l:
                case GestureEventType.m:
                case GestureEventType.n:
                case GestureEventType.o:
                case GestureEventType.p:
                case GestureEventType.q:
                case GestureEventType.r:
                case GestureEventType.s:
                case GestureEventType.t:
                case GestureEventType.u:
                case GestureEventType.v:
                case GestureEventType.w:
                case GestureEventType.x:
                case GestureEventType.y:
                case GestureEventType.z:
                    result += gestureEventType; break;
                default:
                    result += "{" + gestureEventType + "}"; break;
            }

            return result;
        }

        public override string ToString()
        {
            var result = "";
            result += "TRIGGERED BY " + TriggerGestureName;
            result += " " + EventPart1.ToString() + " " + EventPart2.ToString();

            return result;
        }

        internal void SendKeyboardEvents()
        {
            var keysCode = "";
            // first handle the keyboard cases
            // means EventPart1 is a keyboard input, 8 is de index of the last mouse input
            if ((int)EventPart1 > 8) keysCode += this.MakeKeyboardCode(EventPart1);
            if ((int)EventPart2 > 8) keysCode += this.MakeKeyboardCode(EventPart2);
            SendKeys.SendWait(keysCode);
        }

        // this function updates mouse input on all mouse events
        // some events must be sent once the gesture is completed
        // other events must be sent according to the gesture progress
        // progress ranges from 0 to 1
        internal void UpdateMouseMotionInput(double progress, ref MouseEventsHelper.MOUSEINPUT mouse)
        {
            // these cases are mouse events which are related to the gesture progress
            if ((int)EventPart1 > 3 && (int)EventPart1 <= 8) MouseEventsHelper.AddMouseRollEvent(EventPart1, progress, ref mouse);
            if ((int)EventPart2 > 3 && (int)EventPart2 <= 8) MouseEventsHelper.AddMouseRollEvent(EventPart2, progress, ref mouse);
        }

        public GestureEventType EventPart1;
        public GestureEventType EventPart2;
        public string TriggerGestureName;

        internal void SendMouseButtonsInput()
        {
            // cases 1, 2 and 3 are mouse click cases, called once gesture is completed
            if ((int)EventPart1 > 0 && (int)EventPart1 <= 3) MouseEventsHelper.SendMouseClickEvent(EventPart1);
            if ((int)EventPart2 > 0 && (int)EventPart2 <= 3) MouseEventsHelper.SendMouseClickEvent(EventPart2);
        }
    }
}
