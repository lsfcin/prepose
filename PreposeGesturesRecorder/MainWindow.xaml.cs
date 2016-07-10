//------------------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace Microsoft.Samples.Kinect.BodyBasics
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using System.Reflection;
    using System.Linq;
    using Microsoft.Kinect;
    using System.Windows.Media.Media3D;
    using PreposeGestureRecognizer;

    public enum ActionType { Put, Align, Touch, Point, Rotate }

    public static class EnumUtil
    {
        public static IEnumerable<T> GetValues<T>()
        {
            return Enum.GetValues(typeof(T)).Cast<T>();
        }

        public static string GetDescription<T>(this T enumerationValue)
            where T : struct
        {
            Type type = enumerationValue.GetType();
            if (!type.IsEnum)
            {
                throw new ArgumentException("EnumerationValue must be of Enum type", "enumerationValue");
            }

            //Tries to find a DescriptionAttribute for a potential friendly name
            //for the enum
            MemberInfo[] memberInfo = type.GetMember(enumerationValue.ToString());
            if (memberInfo != null && memberInfo.Length > 0)
            {
                object[] attrs = memberInfo[0].GetCustomAttributes(typeof(DescriptionAttribute), false);

                if (attrs != null && attrs.Length > 0)
                {
                    //Pull out the description value
                    return ((DescriptionAttribute)attrs[0]).Description;
                }
            }
            //If we have no description attribute, just return the ToString of the enum
            return enumerationValue.ToString();
        }
    }

    /// <summary>
    /// Interaction logic for MainWindow
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        /// <summary>
        /// Radius of drawn hand circles
        /// </summary>
        private const double HandSize = 30;

        /// <summary>
        /// Thickness of drawn joint lines
        /// </summary>
        private const double JointThickness = 3;

        /// <summary>
        /// Thickness of clip edge rectangles
        /// </summary>
        private const double ClipBoundsThickness = 10;

        /// <summary>
        /// Constant for clamping Z values of camera space points from being negative
        /// </summary>
        private const float InferredZPositionClamp = 0.1f;

        /// <summary>
        /// Brush used for drawing hands that are currently tracked as closed
        /// </summary>
        private readonly Brush handClosedBrush = new SolidColorBrush(Color.FromArgb(128, 255, 0, 0));

        /// <summary>
        /// Brush used for drawing hands that are currently tracked as opened
        /// </summary>
        private readonly Brush handOpenBrush = new SolidColorBrush(Color.FromArgb(128, 0, 255, 0));

        /// <summary>
        /// Brush used for drawing hands that are currently tracked as in lasso (pointer) position
        /// </summary>
        private readonly Brush handLassoBrush = new SolidColorBrush(Color.FromArgb(128, 0, 0, 255));

        /// <summary>
        /// Brush used for drawing joints that are currently tracked
        /// </summary>
        private readonly Brush trackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 68, 192, 68));

        /// <summary>
        /// Brush used for drawing joints that are currently inferred
        /// </summary>        
        private readonly Brush inferredJointBrush = Brushes.Yellow;

        /// <summary>
        /// Pen used for drawing bones that are currently inferred
        /// </summary>        
        private readonly Pen inferredBonePen = new Pen(Brushes.Gray, 1);

        /// <summary>
        /// Drawing group for body rendering output
        /// </summary>
        private DrawingGroup drawingGroup;

        /// <summary>
        /// Drawing image that we will display
        /// </summary>
        private DrawingImage imageSource;

        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor kinectSensor = null;

        /// <summary>
        /// Coordinate mapper to map one type of point to another
        /// </summary>
        private CoordinateMapper coordinateMapper = null;

        /// <summary>
        /// Reader for body frames
        /// </summary>
        private BodyFrameReader bodyFrameReader = null;

        /// <summary>
        /// Array for the bodies
        /// </summary>
        private Body[] bodies = null;

        /// <summary>
        /// definition of bones
        /// </summary>
        private List<Tuple<JointType, JointType>> bones;

        /// <summary>
        /// Width of display (depth space)
        /// </summary>
        private int displayWidth;

        /// <summary>
        /// Height of display (depth space)
        /// </summary>
        private int displayHeight;

        /// <summary>
        /// List of colors for each body tracked
        /// </summary>
        private List<Pen> bodyColors;

        /// <summary>
        /// Current status text to display
        /// </summary>
        private string statusText = null;

        /// <summary>
        /// Timer for FPS calculation
        /// </summary>
        private Stopwatch stopwatch = null;

        Dictionary<JointType, bool> selectedJoints;
        Dictionary<ActionType, bool> selectedActions;
        List<Dictionary<JointType, Vector3D>> checkpoints;


        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {
            // create a vector of joint sets to store checkpoints
            this.checkpoints = new List<Dictionary<JointType, Vector3D>>();

            // create a stopwatch for FPS calculation
            this.stopwatch = new Stopwatch();

            // one sensor is currently supported
            this.kinectSensor = KinectSensor.GetDefault();

            // get the coordinate mapper
            this.coordinateMapper = this.kinectSensor.CoordinateMapper;

            // get the depth (display) extents
            FrameDescription frameDescription = this.kinectSensor.DepthFrameSource.FrameDescription;

            // get size of joint space
            this.displayWidth = frameDescription.Width;
            this.displayHeight = frameDescription.Height;

            // open the reader for the body frames
            this.bodyFrameReader = this.kinectSensor.BodyFrameSource.OpenReader();

            // a bone defined as a line between two joints
            this.bones = new List<Tuple<JointType, JointType>>();

            // Torso
            this.bones.Add(new Tuple<JointType, JointType>(JointType.Head, JointType.Neck));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.Neck, JointType.SpineShoulder));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineShoulder, JointType.SpineMid));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineMid, JointType.SpineBase));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineShoulder, JointType.ShoulderRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineShoulder, JointType.ShoulderLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineBase, JointType.HipRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineBase, JointType.HipLeft));

            // Right Arm
            this.bones.Add(new Tuple<JointType, JointType>(JointType.ShoulderRight, JointType.ElbowRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.ElbowRight, JointType.WristRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.WristRight, JointType.HandRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.HandRight, JointType.HandTipRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.WristRight, JointType.ThumbRight));

            // Left Arm
            this.bones.Add(new Tuple<JointType, JointType>(JointType.ShoulderLeft, JointType.ElbowLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.ElbowLeft, JointType.WristLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.WristLeft, JointType.HandLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.HandLeft, JointType.HandTipLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.WristLeft, JointType.ThumbLeft));

            // Right Leg
            this.bones.Add(new Tuple<JointType, JointType>(JointType.HipRight, JointType.KneeRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.KneeRight, JointType.AnkleRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.AnkleRight, JointType.FootRight));

            // Left Leg
            this.bones.Add(new Tuple<JointType, JointType>(JointType.HipLeft, JointType.KneeLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.KneeLeft, JointType.AnkleLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.AnkleLeft, JointType.FootLeft));

            // populate body colors, one for each BodyIndex
            this.bodyColors = new List<Pen>();

            this.bodyColors.Add(new Pen(Brushes.Red, 6));
            this.bodyColors.Add(new Pen(Brushes.Orange, 6));
            this.bodyColors.Add(new Pen(Brushes.Green, 6));
            this.bodyColors.Add(new Pen(Brushes.Blue, 6));
            this.bodyColors.Add(new Pen(Brushes.Indigo, 6));
            this.bodyColors.Add(new Pen(Brushes.Violet, 6));

            // set IsAvailableChanged event notifier
            this.kinectSensor.IsAvailableChanged += this.Sensor_IsAvailableChanged;

            // open the sensor
            this.kinectSensor.Open();

            // set the status text
            this.StatusText = this.kinectSensor.IsAvailable ? Properties.Resources.RunningStatusText
                                                            : Properties.Resources.NoSensorStatusText;

            // Create the drawing group we'll use for drawing
            this.drawingGroup = new DrawingGroup();

            // Create an image source that we can use in our image control
            this.imageSource = new DrawingImage(this.drawingGroup);

            // use the window object as the view model in this simple example
            this.DataContext = this;

            // initialize the components (controls) of the window
            this.InitializeComponent();
        }

        /// <summary>
        /// INotifyPropertyChangedPropertyChanged event to allow window controls to bind to changeable data
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets the bitmap to display
        /// </summary>
        public ImageSource ImageSource
        {
            get
            {
                return this.imageSource;
            }
        }

        /// <summary>
        /// Gets or sets the current status text to display
        /// </summary>
        public string StatusText
        {
            get
            {
                return this.statusText;
            }

            set
            {
                if (this.statusText != value)
                {
                    this.statusText = value;

                    // notify any bound elements that the text has changed
                    if (this.PropertyChanged != null)
                    {
                        this.PropertyChanged(this, new PropertyChangedEventArgs("StatusText"));
                    }
                }
            }
        }

        /// <summary>
        /// Execute start up tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (this.bodyFrameReader != null)
            {
                this.bodyFrameReader.FrameArrived += this.Reader_FrameArrived;
            }

            var jointTypes = EnumUtil.GetValues<JointType>();

            selectedJoints = new Dictionary<JointType, bool>();
            foreach (var jointType in jointTypes)
                selectedJoints.Add(jointType, false);

            var actionTypes = EnumUtil.GetValues<ActionType>();
            selectedActions = new Dictionary<ActionType, bool>();
            foreach (var actionType in actionTypes)
                selectedActions.Add(actionType, false);

            UpdateSelectedJoints();
            UpdateSelectedActions();
        }

        private void UpdateSelectedJoints()
        {
            foreach (var child in RecordingSettingsGrid.Children)
            {
                if(child is System.Windows.Controls.CheckBox)
                {
                    var checkbox = (System.Windows.Controls.CheckBox)child;
                    switch((string)(checkbox.Content))
                    {
                        case "Head": selectedJoints[JointType.Head] = (bool)checkbox.IsChecked; break;
                        case "Neck": selectedJoints[JointType.Neck] = (bool)checkbox.IsChecked; break;
                        case "M. Shoulder": selectedJoints[JointType.SpineShoulder] = (bool)checkbox.IsChecked; break;
                        case "M. Hip": selectedJoints[JointType.SpineBase] = (bool)checkbox.IsChecked; break;
                        case "M. Spine": selectedJoints[JointType.SpineMid] = (bool)checkbox.IsChecked; break;
                        case "L. Shoulder": selectedJoints[JointType.ShoulderLeft] = (bool)checkbox.IsChecked; break;
                        case "L. Elbow": selectedJoints[JointType.ElbowLeft] = (bool)checkbox.IsChecked; break;
                        case "L. Hip": selectedJoints[JointType.HipLeft] = (bool)checkbox.IsChecked; break;
                        case "L. Wrist": selectedJoints[JointType.WristLeft] = (bool)checkbox.IsChecked; break;
                        case "L. Hand": selectedJoints[JointType.HandLeft] = (bool)checkbox.IsChecked; break;
                        case "L. Hand Tip": selectedJoints[JointType.HandTipLeft] = (bool)checkbox.IsChecked; break;
                        case "L. Knee": selectedJoints[JointType.KneeLeft] = (bool)checkbox.IsChecked; break;
                        case "L. Ankle": selectedJoints[JointType.AnkleLeft] = (bool)checkbox.IsChecked; break;
                        case "L. Foot": selectedJoints[JointType.FootLeft] = (bool)checkbox.IsChecked; break;
                        case "L. Thumb": selectedJoints[JointType.ThumbLeft] = (bool)checkbox.IsChecked; break;
                        case "R. Shoulder": selectedJoints[JointType.ShoulderRight] = (bool)checkbox.IsChecked; break;
                        case "R. Elbow": selectedJoints[JointType.ElbowRight] = (bool)checkbox.IsChecked; break;
                        case "R. Hip": selectedJoints[JointType.HipRight] = (bool)checkbox.IsChecked; break;
                        case "R. Wrist": selectedJoints[JointType.WristRight] = (bool)checkbox.IsChecked; break;
                        case "R. Hand": selectedJoints[JointType.HandRight] = (bool)checkbox.IsChecked; break;
                        case "R. Hand Tip": selectedJoints[JointType.HandTipRight] = (bool)checkbox.IsChecked; break;
                        case "R. Thumb": selectedJoints[JointType.ThumbRight] = (bool)checkbox.IsChecked; break;
                        case "R. Knee": selectedJoints[JointType.KneeRight] = (bool)checkbox.IsChecked; break;
                        case "R. Ankle": selectedJoints[JointType.AnkleRight] = (bool)checkbox.IsChecked; break;
                        case "R. Foot": selectedJoints[JointType.FootRight] = (bool)checkbox.IsChecked; break;
                    }
                }
            }
        }

        private void UpdateSelectedActions()
        {
            foreach (var child in RecordingSettingsGrid.Children)
            {
                if (child is System.Windows.Controls.CheckBox)
                {
                    var checkbox = (System.Windows.Controls.CheckBox)child;
                    switch ((string)(checkbox.Content))
                    {
                        case "Put": selectedActions[ActionType.Put] = (bool)checkbox.IsChecked; break;
                        case "Align": selectedActions[ActionType.Align] = (bool)checkbox.IsChecked; break;
                        case "Touch": selectedActions[ActionType.Touch] = (bool)checkbox.IsChecked; break;
                        case "Point": selectedActions[ActionType.Point] = (bool)checkbox.IsChecked; break;
                        case "Rotate": selectedActions[ActionType.Rotate] = (bool)checkbox.IsChecked; break;
                    }
                }
            }
        }


        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (this.bodyFrameReader != null)
            {
                // BodyFrameReader is IDisposable
                this.bodyFrameReader.Dispose();
                this.bodyFrameReader = null;
            }

            if (this.kinectSensor != null)
            {
                this.kinectSensor.Close();
                this.kinectSensor = null;
            }
        }

        /// <summary>
        /// Handles the body frame data arriving from the sensor
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Reader_FrameArrived(object sender, BodyFrameArrivedEventArgs e)
        {
            bool dataReceived = false;

            using (BodyFrame bodyFrame = e.FrameReference.AcquireFrame())
            {
                if (bodyFrame != null)
                {
                    if (this.bodies == null)
                    {
                        this.bodies = new Body[bodyFrame.BodyCount];
                    }

                    // The first time GetAndRefreshBodyData is called, Kinect will allocate each Body in the array.
                    // As long as those body objects are not disposed and not set to null in the array,
                    // those body objects will be re-used.
                    bodyFrame.GetAndRefreshBodyData(this.bodies);
                    dataReceived = true;
                }
            }

            if (dataReceived)
            {
                using (DrawingContext dc = this.drawingGroup.Open())
                {
                    // Draw a transparent background to set the render size
                    dc.DrawRectangle(Brushes.Black, null, new Rect(0.0, 0.0, this.displayWidth, this.displayHeight));

                    var penIndex = 0;
                    var drawPen = this.bodyColors[penIndex];

                    // first choose the nearest body
                    var body = GetNearestBody();
                    if (body != null)
                    {
                        this.DrawClippedEdges(body, dc);

                        IReadOnlyDictionary<JointType, Joint> joints = body.Joints;

                        // convert the joint points to depth (display) space
                        Dictionary<JointType, Point> jointPoints = new Dictionary<JointType, Point>();

                        foreach (JointType jointType in joints.Keys)
                        {
                            // sometimes the depth(Z) of an inferred joint may show as negative
                            // clamp down to 0.1f to prevent coordinatemapper from returning (-Infinity, -Infinity)
                            CameraSpacePoint position = joints[jointType].Position;
                            if (position.Z < 0)
                            {
                                position.Z = InferredZPositionClamp;
                            }

                            DepthSpacePoint depthSpacePoint = this.coordinateMapper.MapCameraPointToDepthSpace(position);
                            jointPoints[jointType] = new Point(depthSpacePoint.X, depthSpacePoint.Y);
                        }

                        this.DrawBody(joints, jointPoints, dc, drawPen);

                        this.DrawHand(body.HandLeftState, jointPoints[JointType.HandLeft], dc);
                        this.DrawHand(body.HandRightState, jointPoints[JointType.HandRight], dc);

                        if (stopwatch.IsRunning)
                            UpdateRecordingStatus(body);
                    }

                    // prevent drawing outside of our render area
                    this.drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, this.displayWidth, this.displayHeight));
                }
            }
        }

        private void UpdateRecordingStatus(Body body)
        {
            var start = Int32.Parse(StartAfterTextBox.Text);
            var length = Int32.Parse(RecordForTextBox.Text);
            var elapsed = stopwatch.ElapsedMilliseconds / 1000;
            if (elapsed < start)
            {
                var remaining = start - elapsed;
                RecordingStatus.Text = "Recording in " + remaining + " seconds...";
                RecordedCodeTextBox.Text = "Recording in " + remaining + " seconds...";

            }
            else if (elapsed >= start && elapsed < start + length)
            {
                var remaining = (start + length) - elapsed;
                RecordingStatus.Text = "Recording will finish in " + remaining + " seconds...";
                RecordedCodeTextBox.Text = "Recording will finish in " + remaining + " seconds...";

                UpdateCheckpoints(body);
            }
            else
            {
                StopButton_Click(null, null);

                WritePreposeCode();                
            }
        }

        private void WritePreposeCode()
        {
            RecordedCodeTextBox.Text = "APP app_name:";
            RecordedCodeTextBox.Text += "\n  GESTURE gesture_name:";
            var poseCount = 0;
            foreach(var checkpoint in checkpoints)
            {
                poseCount++;
                RecordedCodeTextBox.Text += "\n    POSE pose_" + poseCount + ":";
                foreach(var selectedJoint in selectedJoints)
                {
                    foreach(var selectedAction in selectedActions)
                    {
                        if(selectedAction.Value)
                        {
                            switch(selectedAction.Key)
                            {
                                case ActionType.Put:
                                    WritePutAction(checkpoint, selectedJoint);
                                    break;
                                case ActionType.Align:
                                    break;
                                case ActionType.Touch:
                                    break;
                                case ActionType.Point:
                                    break;
                                case ActionType.Rotate:
                                    break;
                            }
                        }
                    }
                }
            }
        }

        private string WritePutAction(Dictionary<JointType, Vector3D> bodyVectors, KeyValuePair<JointType, bool> currentJoint)
        {
            var result = "";

            foreach(var selectedJoint in selectedJoints)
            {
                // selectedJoint must be active
                // and in order to relate joints in only one way
                // currentJoint key must be greater than selectedJoint key
                if(selectedJoint.Value && currentJoint.Key > selectedJoint.Key)
                {
                    // get both joints positions
                    var currentPosition = CalcPosition(bodyVectors, currentJoint);

                    result += "put " + WriteJoint(currentJoint.Key);

                }
            }

            return result;
        }

        private Vector3D CalcPosition(Dictionary<JointType, Vector3D> bodyVectors, KeyValuePair<JointType, bool> currentJoint)
        {
            throw new NotImplementedException();
        }

        private string WriteJoint(JointType jointType)
        {
            var result = "";

            switch(jointType)
            {
                case JointType.Neck: result += "your neck"; break;
                case JointType.Head: result += "your head"; break;
                case JointType.SpineMid: result += "your spine mid"; break;
                case JointType.SpineBase: result += "your spine base"; break;
                case JointType.SpineShoulder: result += "your spine shoulder"; break;
                case JointType.ShoulderLeft: result += "your left shoulder"; break;
                case JointType.ElbowLeft: result += "your left elbow"; break;
                case JointType.WristLeft: result += "your left wrist"; break;
                case JointType.HandLeft: result += "your left hand"; break;
                case JointType.HandTipLeft: result += "your left hand tip"; break;
                case JointType.ThumbLeft: result += "your left thumb"; break;
                case JointType.HipLeft: result += "your left hip"; break;
                case JointType.KneeLeft: result += "your left knee"; break;
                case JointType.AnkleLeft: result += "your left ankle"; break;
                case JointType.FootLeft: result += "your left foot"; break;
                case JointType.ShoulderRight: result += "your right shoulder"; break;
                case JointType.ElbowRight: result += "your right elbow"; break;
                case JointType.WristRight: result += "your right wrist"; break;
                case JointType.HandRight: result += "your right hand"; break;
                case JointType.HandTipRight: result += "your right hand tip"; break;
                case JointType.ThumbRight: result += "your right thumb"; break;
                case JointType.HipRight: result += "your right hip"; break;
                case JointType.KneeRight: result += "your right knee"; break;
                case JointType.AnkleRight: result += "your right ankle"; break;
                case JointType.FootRight: result += "your right foot"; break;
            }

            return result;
        }

        private void UpdateCheckpoints(Body body)
        {
            // maximum accepted angle in degrees
            var maxAngle = 45.0;

            var joints =
                Z3KinectConverter.KinectToHipsSpineCoordinateSystem(body.Joints);

            // if there is no checkpoint recorded add joints as the first one
            if (checkpoints.Count == 0)
            {
                checkpoints.Add(joints);
            }
            else
            {
                RecordedCodeTextBox.Text = "joints feedbacks\n";

                // check if current joints are accepted
                // by the last checkpoint
                foreach(var selectedJoint in selectedJoints)
                {
                    if(selectedJoint.Value)
                    {
                        var jointType = selectedJoint.Key;
                        var v1 = checkpoints.Last()[jointType];
                        var v2 = joints[jointType];
                        var angle = Vector3D.AngleBetween(v1, v2);
                        
                        RecordedCodeTextBox.Text += "j " + jointType + " angle: " + angle + "\n";
                        // if a single selected joint is too far
                        // than create a new checkpoint
                        if(angle > maxAngle)
                        {
                            checkpoints.Add(joints);
                            break;
                        }                        
                    }
                }
            }
        }

        private Body GetNearestBody()
        {
            Body result = null;
            foreach (var body in this.bodies)
            {
                if (body.IsTracked)
                {
                    if (result == null)
                        result = body;
                    else if (body.Joints[JointType.SpineBase].Position.Z < result.Joints[JointType.SpineBase].Position.Z)
                        result = body;
                }
            }
            return result;
        }

        /// <summary>
        /// Draws a body
        /// </summary>
        /// <param name="joints">joints to draw</param>
        /// <param name="jointPoints">translated positions of joints to draw</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        /// <param name="drawingPen">specifies color to draw a specific body</param>
        private void DrawBody(IReadOnlyDictionary<JointType, Joint> joints, IDictionary<JointType, Point> jointPoints, DrawingContext drawingContext, Pen drawingPen)
        {
            // Draw the bones
            foreach (var bone in this.bones)
            {
                this.DrawBone(joints, jointPoints, bone.Item1, bone.Item2, drawingContext, drawingPen);
            }

            // Draw the joints
            foreach (JointType jointType in joints.Keys)
            {
                Brush drawBrush = null;

                TrackingState trackingState = joints[jointType].TrackingState;

                if (trackingState == TrackingState.Tracked)
                {
                    drawBrush = this.trackedJointBrush;
                }
                else if (trackingState == TrackingState.Inferred)
                {
                    drawBrush = this.inferredJointBrush;
                }

                if (drawBrush != null)
                {
                    drawingContext.DrawEllipse(drawBrush, null, jointPoints[jointType], JointThickness, JointThickness);
                }
            }
        }

        /// <summary>
        /// Draws one bone of a body (joint to joint)
        /// </summary>
        /// <param name="joints">joints to draw</param>
        /// <param name="jointPoints">translated positions of joints to draw</param>
        /// <param name="jointType0">first joint of bone to draw</param>
        /// <param name="jointType1">second joint of bone to draw</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        /// /// <param name="drawingPen">specifies color to draw a specific bone</param>
        private void DrawBone(IReadOnlyDictionary<JointType, Joint> joints, IDictionary<JointType, Point> jointPoints, JointType jointType0, JointType jointType1, DrawingContext drawingContext, Pen drawingPen)
        {
            Joint joint0 = joints[jointType0];
            Joint joint1 = joints[jointType1];

            // If we can't find either of these joints, exit
            if (joint0.TrackingState == TrackingState.NotTracked ||
                joint1.TrackingState == TrackingState.NotTracked)
            {
                return;
            }

            // We assume all drawn bones are inferred unless BOTH joints are tracked
            Pen drawPen = this.inferredBonePen;
            if ((joint0.TrackingState == TrackingState.Tracked) && (joint1.TrackingState == TrackingState.Tracked))
            {
                drawPen = drawingPen;
            }

            drawingContext.DrawLine(drawPen, jointPoints[jointType0], jointPoints[jointType1]);
        }

        /// <summary>
        /// Draws a hand symbol if the hand is tracked: red circle = closed, green circle = opened; blue circle = lasso
        /// </summary>
        /// <param name="handState">state of the hand</param>
        /// <param name="handPosition">position of the hand</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private void DrawHand(HandState handState, Point handPosition, DrawingContext drawingContext)
        {
            switch (handState)
            {
                case HandState.Closed:
                    drawingContext.DrawEllipse(this.handClosedBrush, null, handPosition, HandSize, HandSize);
                    break;

                case HandState.Open:
                    drawingContext.DrawEllipse(this.handOpenBrush, null, handPosition, HandSize, HandSize);
                    break;

                case HandState.Lasso:
                    drawingContext.DrawEllipse(this.handLassoBrush, null, handPosition, HandSize, HandSize);
                    break;
            }
        }

        /// <summary>
        /// Draws indicators to show which edges are clipping body data
        /// </summary>
        /// <param name="body">body to draw clipping information for</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private void DrawClippedEdges(Body body, DrawingContext drawingContext)
        {
            FrameEdges clippedEdges = body.ClippedEdges;

            if (clippedEdges.HasFlag(FrameEdges.Bottom))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, this.displayHeight - ClipBoundsThickness, this.displayWidth, ClipBoundsThickness));
            }

            if (clippedEdges.HasFlag(FrameEdges.Top))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, this.displayWidth, ClipBoundsThickness));
            }

            if (clippedEdges.HasFlag(FrameEdges.Left))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, ClipBoundsThickness, this.displayHeight));
            }

            if (clippedEdges.HasFlag(FrameEdges.Right))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(this.displayWidth - ClipBoundsThickness, 0, ClipBoundsThickness, this.displayHeight));
            }
        }

        /// <summary>
        /// Handles the event which the sensor becomes unavailable (E.g. paused, closed, unplugged).
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Sensor_IsAvailableChanged(object sender, IsAvailableChangedEventArgs e)
        {
            // on failure, set the status text
            this.StatusText = this.kinectSensor.IsAvailable ? Properties.Resources.RunningStatusText
                                                            : Properties.Resources.SensorNotAvailableStatusText;
        }

        private void RecordButton_Click(object sender, RoutedEventArgs e)
        {
            RecordingStatus.Text = "Recording in " + StartAfterTextBox.Text + " seconds...";
            RecordedCodeTextBox.Text = "Recording in " + StartAfterTextBox.Text + " seconds..."; 
            RecordButton.Visibility = System.Windows.Visibility.Hidden;
            StopButton.Visibility = System.Windows.Visibility.Visible;
            RecordingSettingsGrid.IsEnabled = false;
            MainTabControl.SelectedItem = CodeTab;

            if (this.stopwatch.IsRunning || this.stopwatch.ElapsedMilliseconds > 0) 
                this.stopwatch.Reset();

            checkpoints.Clear();

            this.stopwatch.Start();
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            RecordingStatus.Text = "Press button to record.";

            RecordButton.Visibility = System.Windows.Visibility.Visible;
            StopButton.Visibility = System.Windows.Visibility.Hidden;
            RecordingSettingsGrid.IsEnabled = true;

            this.stopwatch.Stop();
        }

        private void TransformsRadioButton_Click(object sender, RoutedEventArgs e)
        {
            PointTransformCheckBox.IsEnabled = true;
            PointTransformCheckBox.IsChecked = true;
            RotateTransformCheckBox.IsEnabled = true;
            RotateTransformCheckBox.IsChecked = true;

            PutRestrictionCheckBox.IsEnabled = false;
            PutRestrictionCheckBox.IsChecked = false;
            AlignRestrictionCheckBox.IsEnabled = false;
            AlignRestrictionCheckBox.IsChecked = false;
            TouchRestrictionCheckBox.IsEnabled = false;
            TouchRestrictionCheckBox.IsChecked = false;

            UpdateSelectedActions();
        }

        private void RestrictionsRadioButton_Click(object sender, RoutedEventArgs e)
        {
            PutRestrictionCheckBox.IsEnabled = true;
            PutRestrictionCheckBox.IsChecked = true;
            AlignRestrictionCheckBox.IsEnabled = true;
            AlignRestrictionCheckBox.IsChecked = false;
            TouchRestrictionCheckBox.IsEnabled = true;
            TouchRestrictionCheckBox.IsChecked = false;

            PointTransformCheckBox.IsEnabled = false;
            PointTransformCheckBox.IsChecked = false;
            RotateTransformCheckBox.IsEnabled = false;
            RotateTransformCheckBox.IsChecked = false;

            UpdateSelectedActions();
        }

        private void ActionCheckBox_Click(object sender, RoutedEventArgs e)
        {
            UpdateSelectedActions();
        }

        private void JointCheckBox_Click(object sender, RoutedEventArgs e)
        {
            UpdateSelectedJoints();
        }
    }
}
