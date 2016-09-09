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
                this.ProgressBar.Value = 1.0; // set to 100% directly
                this.Stopwatch.Restart(); // restart MainInstruction cooldown because gesture was completed                
            }
            else
            {
                if (this.ProgressBar.Value == 1) // it means the gesture was completed on the last frame
                {                    
                    this.ProgressBar.Value = 0.0;
                }
                else if (status.AccumulatedError >= 1.0) // it means gesture was broken
                {
                    this.ProgressBar.Value = 0.0;
                }
                else
                {
                    this.ProgressBar.Value = this.ProgressBar.Value * 0.5 + GesturePercentage * 0.5;
                }
            }

            // set FailureBar.Value
            if (status.Broke)
            {
                this.FailureBar.Value = 1.0;
                this.FailureBar.Foreground = Brushes.Red;
                this.Stopwatch.Restart(); // restart MainInstruction cooldown because gesture was broke                
            }
            else if(GesturePercentage >= 1)
            {
                this.FailureBar.Value = 0.0;
            }
            else
            {
                this.FailureBar.Value = this.FailureBar.Value * 0.5 + status.AccumulatedError * 0.5;
                this.FailureBar.Foreground = Brushes.Orange;
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
