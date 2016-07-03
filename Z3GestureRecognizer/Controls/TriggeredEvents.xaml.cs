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
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PreposeGestureRecognizer.Controls
{
    /// <summary>
    /// Interaction logic for TriggeredEvents.xaml
    /// </summary>
    public partial class TriggeredEvents : System.Windows.Controls.UserControl
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

            result.EventPart1 = (GestureEventType) this.Event1ComboBox.SelectedValue;
            result.EventPart2 = (GestureEventType) this.Event2ComboBox.SelectedValue;

            return result;
        }

        public void SetEvent(GestureEvent gestureEvent)
        {
            this.Event1ComboBox.SelectedValue = gestureEvent.EventPart1;
            this.Event2ComboBox.SelectedValue = gestureEvent.EventPart2;
        }
    }
}
