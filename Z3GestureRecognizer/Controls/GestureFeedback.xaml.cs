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
    /// <summary>
    /// Interaction logic for GestureProgress.xaml
    /// </summary>
    public partial class GestureFeedback : UserControl
    {
        public GestureFeedback(Gesture gesture)
        {
            InitializeComponent();
            this.Gesture = gesture;
            this.CurrentStepTextBlock.Text = gesture.Steps[0].Pose.ToString();
            this.MainGestureAndPoseNameTextBlock.Text = gesture.Name;
        }

        public void StopMainProgressBars()
        {
            foreach (var bar in ProgressBars)
            {
                this.MainGrid.Children.Remove(bar);
            }

            foreach (var textBlock in PosesTextBlocks)
            {
                this.MainGrid.Children.Remove(textBlock);
            }

            ProgressBars.Clear();
            PosesTextBlocks.Clear();
        }

        public void RenderFeedback(GestureStatus status)
        {
            var GesturePercentage = status.GetGesturePercentage();

            this.ProgressBar.Value = GesturePercentage * 100;
            this.CurrentStepTextBlock.Text = status.StepNamesAndDescriptions[status.CurrentStep].Item2;
            this.CompletedTimesTextBlock.Text = status.CompletedCount.ToString();
            this.Height = 
                this.MainGestureAndPoseNameTextBlock.ActualHeight +
                this.ProgressBar.ActualHeight +
                this.CurrentStepTextBlock.ActualHeight +
                10;
        }

        public Gesture Gesture { get; set; }
        private static List<System.Windows.Controls.ProgressBar> ProgressBars = null;
        private static List<TextBlock> PosesTextBlocks = null;
    }
}
