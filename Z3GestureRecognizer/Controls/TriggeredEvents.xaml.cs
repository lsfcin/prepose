using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using PreposeGestures;

namespace PreposeGestureRecognizer.Controls
{
    public enum GestureEventType {
        NONE, SHIFT, CTRL, ALT, BACKSPACE, CAPSLOCK, DELETE, ENTER, ESC, TAB, UP, DOWN, LEFT, RIGHT, HOME, END, PGUP, PGDN,
        F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,
        a, b, c, d, e, f, g, h, i, j, k, l, m, n, o, p, q, r, s, t, u, v, y, x, w, z
    };

    public class GestureEvent
    {
        public GestureEvent()
        {
            Part1 = GestureEventType.NONE;
            Part2 = GestureEventType.NONE;
        }

        public GestureEvent(string part1String, string part2String)
        {
            Part1 = (GestureEventType)Enum.Parse(typeof(GestureEventType), part1String);
            Part2 = (GestureEventType)Enum.Parse(typeof(GestureEventType), part2String);
        }

        public string MakeCode()
        {
            var result = "";

            result += this.MakeCode(Part1);
            result += this.MakeCode(Part2);

            return result;
        }
        public string MakeCode(GestureEventType gestureEventType)
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

            result += Part1.ToString() + " " + Part2.ToString();

            return result;
        }

        public GestureEventType Part1;
        public GestureEventType Part2;
    }

    /// <summary>
    /// Interaction logic for TriggeredEvents.xaml
    /// </summary>
    public partial class TriggeredEvents : UserControl
    {
        public TriggeredEvents()
        {
            InitializeComponent();

            this.Event1ComboBox.ItemsSource = Enum.GetValues(typeof(GestureEventType)).Cast<GestureEventType>();
            this.Event2ComboBox.ItemsSource = Enum.GetValues(typeof(GestureEventType)).Cast<GestureEventType>();
        }

        public GestureEvent GetEvent()
        {
            var result = new GestureEvent();

            result.Part1 = (GestureEventType) this.Event1ComboBox.SelectedValue;
            result.Part2 = (GestureEventType) this.Event2ComboBox.SelectedValue;

            return result;
        }

        public void SetEvent(GestureEvent gestureEvent)
        {
            this.Event1ComboBox.SelectedValue = gestureEvent.Part1;
            this.Event2ComboBox.SelectedValue = gestureEvent.Part2;
        }
    }
}
