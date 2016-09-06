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
using System.Diagnostics;

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
            this.CurrentStepTextBlock.Text = gesture.Steps[0].Pose.Name;
            this.MainGestureAndPoseNameTextBlock.Text = gesture.Name;            
            this.Stopwatch = new Stopwatch();
            this.Stopwatch.Start();
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

            // set ProgressBar.Value
            var GesturePercentage = status.GetGesturePercentage();            
            if(status.Succeeded)
            {
                this.ProgressBar.Value = 100.0; // set to 100% directly
                this.Stopwatch.Restart(); // also restart MainInstruction cooldown because gesture was completed                
            }
            else
            {
                if(this.ProgressBar.Value == 100) // it means last frame gesture was completed, then reset .Value
                {                    
                    this.ProgressBar.Value = 0;
                }
                else
                {
                    this.ProgressBar.Value = this.ProgressBar.Value * 0.8 + GesturePercentage * 20.0;
                }
            }

            // set MainInstruction
            // MainInstruction is updated only after a cooldown 
            // this makes it easier for the user to read the instruction            
            var cooldown = 1000;
            var elapsed = this.Stopwatch.ElapsedMilliseconds;
            if(elapsed > cooldown)
            { 
                this.CurrentStepTextBlock.Text = status.MainInstruction; //Name;
                elapsed = 0;
                this.Stopwatch.Restart();
            }

            // set CompletedCount            
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

        private Stopwatch Stopwatch;
    }
}
