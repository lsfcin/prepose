﻿using Microsoft.Z3;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PreposeGestures
{
    public class BodyMatcher
    {
        public int Precision { get; set; }

        public BodyMatcher(App app, int precision = 15)
        {
            this.App = app;
            this.Precision = precision;
            foreach (var gesture in this.App.Gestures)
            {
                var matcher = new GestureMatcher(gesture);
                Matchers.Add(matcher);
            }
        }

        public BodyMatcher(List<Gesture> gestureList, int precision = 15)
        {
            this.App = null;
            this.Precision = precision;
            foreach (var gesture in gestureList)
            {
                var matcher = new GestureMatcher(gesture);
                Matchers.Add(matcher);

            }
        }

        public void AddGesture(Gesture gesture)
        {
            var matcher = new GestureMatcher(gesture);
            Matchers.Add(matcher);
        }

        internal App App { get; set; }
        internal IList<GestureMatcher> Matchers = new List<GestureMatcher>();

        public List<GestureStatus> TestBody(Z3Body body)
        {            
            var result = new List<GestureStatus>();

            // InitBody synthesizes the initial pose to go after. Synthesizing a pose is _not_ parallel-safe because the Z3 context isn't safe to share across threads
            foreach (var matcher in this.Matchers)
            {
                matcher.InitBody(body);
            }

            // Data parallel -- each matcher is independent of each other, so we can use Task Parallel Library to send each matcher to a different core if necessary
            // The task parallel library should take care of locking the result array for us...let's hope it doesn't serialize by locking result in the wrong way

            /*
            Parallel.ForEach(this.Matchers,
                matcher => {
                    Context Z3ThreadLocalContext = new Context(new Dictionary<string, string>() { { "MODEL", "true" }, { "PROOF_MODE", "2" } });
                    matcher.TestBody(body, this.Precision, Z3ThreadLocalContext);
                    result.Add(matcher.GetStatus());
                }); 
            */

            // Matching itself uses the Z3 context - and that can't be shared across threads! 
            foreach (var matcher in this.Matchers)
            {
                result.Add(matcher.MatchBody(body, this.Precision, Z3.Context));
            }            

            // Check for each matcher to see if it succeeded. If it did, then synthesize a new target position. 
            foreach (var matcher in this.Matchers)
            {
                if (matcher.GetStatus().Succeeded)
                {
                    matcher.UpdateTargetBody(body);
                }
            }            

            return result;
        }


        public GestureStatus GetLastGestureStatus()
        {
            return this.Matchers[this.Matchers.Count - 1].GetStatus();
        }

        public Z3Target GetLastGestureTarget()
        {
            return this.Matchers[this.Matchers.Count - 1].Target;
        }
    }


    internal class GestureMatcher
    {
        internal GestureMatcher(Gesture gesture)
        {
            this.Gesture = gesture;
            this.MainRestriction = "";
            if(gesture.DeclaredPoses[0].RestrictionCount > 0)
                this.MainRestriction = gesture.DeclaredPoses[0].Restriction.ToString();
            this.StepLastPercentage = 0;
            this.CompletedCount = 0;
            this.Target = null;
        }

        public Context GestureMatcherLocalContext;

        public Gesture Gesture { get; set; }
        public uint frameCount = 0;

        public void InitBody(Z3Body body)
        {
            InitBody(body, Z3.Context);
        }

        public void InitBody(Z3Body body, Context localContext)
        {
            GestureMatcherLocalContext = localContext;
            if (this.Target == null)
            {
                this.UpdateTargetBody(body);
            }
        }

        public GestureStatus MatchBody(Z3Body body, int precision, Context localContext)
        {            
            var matchStat = new StatisticsEntryMatch();
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var time0 = stopwatch.ElapsedMilliseconds;

            // NOT THREAD SAFE
            this.LastDistanceVectors = this.UpdateDistanceVectors(body, localContext);

            // Check distance to target transformed joints and if body satisfies restrictions
            var transformsPercentage = 0.0;
            var restrictionsPercentage = 0.0;
            var mainRestriction = "";
            this.CalcPercentages(
                body, 
                precision,
                out transformsPercentage, 
                out restrictionsPercentage, 
                out mainRestriction);
            this.MainRestriction = mainRestriction;

            var gestureSucceeded = false;
            var gestureBroke = false;
            var stepPercentage = Math.Min(transformsPercentage, restrictionsPercentage);
            var performanceError = Math.Max(0.0, this.StepLastPercentage - stepPercentage);
            var stepSucceeded = stepPercentage >= 1.0;
            this.StepLastPercentage = stepPercentage;

            // Increase AccumulatedError only if performanceError is positive
            if (performanceError > 0.00001)
            {
                // delay error only applies if the performance is negative
                var baseError = 0.1; // 0.1 base error allows no more than 0.3 sec of bad performance
                var weightedError = performanceError * 3;
                this.AccumulatedError += baseError + weightedError;
            
                // If accumulated error is too high the gesture is broken
                if (this.AccumulatedError > 1)
                {
                    this.Reset(body);
                    this.AccumulatedError = 0;
                    gestureBroke = true;

                    // Immediatilly update step percentage
                    this.CalcPercentages(
                        body,
                        precision,
                        out transformsPercentage,
                        out restrictionsPercentage,
                        out mainRestriction);

                    this.StepLastPercentage = Math.Min(transformsPercentage, restrictionsPercentage);
                }
            }

            // If body is accepted move to matching the next pose in sequence
            if (stepSucceeded)
            {
                this.CurrentStep += 1;
                this.AccumulatedError = 0;

                // Check if gesture is completed (all steps are finished)
                if (this.CurrentStep >= this.Gesture.Steps.Count)
                {
                    this.Reset(body);
                    this.CompletedCount += 1;
                    gestureSucceeded = true;
                }
                this.UpdateTargetBody(body);
                
                // Immediatilly update step percentage
                this.CalcPercentages(
                    body, 
                    precision, 
                    out transformsPercentage, 
                    out restrictionsPercentage, 
                    out mainRestriction);

                this.StepLastPercentage = Math.Min(transformsPercentage, restrictionsPercentage);
            }
            var result = this.GetStatus();
            result.Broke = gestureBroke;

            // The VisualGestureBuilder API for DiscreteGestureResult cares about whole gestures, 
            // not about individual poses. We need to check for this case explicitly.
            // We then expose succeededDetection in a DiscreteGestureResult . 
            result.Succeeded = gestureSucceeded;

            // TODO: check the semantics of succeededDetectionFirstFrame in the VisualGestureBuilder API
            // We here are assuming that firstFrame means this is the first frame where we successfully detected the gesture
            // We are assuming that firstFrame does NOT mean this is the first frame where we started tracking the gesture
            if (CompletedCount == 1 & gestureSucceeded)
                result.SucceededFirstFrame = true;
            else
                result.SucceededFirstFrame = false;

            // TODO: check the semantics of confidence in the VisualGestureBuilder API.
            // Here confidence is the same as distance from the target body. 
            // That is NOT the same as if confidence were a probability that we are correct
            // We want to have as close semantics as possible to the VisualGestureBuilder API, 
            // so we need to double check this
            result.Confidence = (float) result.GetGesturePercentage();

            matchStat.timeMs = stopwatch.ElapsedMilliseconds - time0;
            matchStat.gestureName = this.Gesture.Name;
            matchStat.uid = frameCount;
            matchStat.poseName = this.Gesture.Steps[CurrentStep].Pose.Name;

            // Record the statistics entry here 
            GestureStatistics.matchTimes.Add(matchStat);
            frameCount++;

            return result;
        }

        private void CalcPercentages(
            Z3Body body, 
            int precision,
            out double transformsPercentage, 
            out double restrictionsPercentage,
            out string mainRestriction)
        {            
            transformsPercentage = 1.0;
            if (this.Target.TransformedJoints.Count > 0)
            {
                transformsPercentage = Math.Min(1.0, 
                    TrigonometryHelper.GetDistance(precision) / 
                    CalcMaxDistance(this.LastDistanceVectors, this.Target.TransformedJoints));
            }

            restrictionsPercentage = 
                this.Gesture.Steps[CurrentStep].Pose.CalcMinPercentage(body, out mainRestriction);
        }

        internal void UpdateTargetBody(Z3Body startBody)
        {
            // delayed statements are a special case
            // they can be treated as restrictions or as transformations
            // in order to the pose to retrieve the composite restrictions and transformations
            // we must first provide the start body to update its delayed statements
            // so they are treated first on the update target function
            this.Gesture.Steps[this.CurrentStep].Pose.UpdateDelayedStatements(startBody);

            var stopwatch = new Stopwatch();
            StatisticsEntrySynthesize synTime = new StatisticsEntrySynthesize();
            stopwatch.Start();
            var time0 = stopwatch.ElapsedMilliseconds;

            if (this.Target != null && this.CurrentStep > 0)
            {
                // If last target is not null than use the target transformed joints instead of the start transformed joints
                // This way no error is cumulated along the transforms due to the matching precisions
                foreach (var jointType in this.Gesture.Steps[this.CurrentStep - 1].Pose.GetTransformJointTypes())
                {
                    // The Z3Point3D depends on a specific Z3 context deep underneath
                    // We need to thread the context through 
                    startBody.Joints[jointType] =
                        new Z3Point3D(
                            Z3Math.GetRealValue(this.Target.Body.Joints[jointType].X),
                            Z3Math.GetRealValue(this.Target.Body.Joints[jointType].Y),
                            Z3Math.GetRealValue(this.Target.Body.Joints[jointType].Z),
                            Z3.Context);
                }
            }

            this.Target = this.Gesture.Steps[this.CurrentStep].Pose.CalcNearestTargetBody(startBody);
            this.LastDistanceVectors = UpdateDistanceVectors(startBody, Z3.Context);

            var time1 = stopwatch.ElapsedMilliseconds - time0;
            stopwatch.Stop();

            synTime.timeMs = time1;
            synTime.gestureName = this.Gesture.Name;
            synTime.poseName = this.Gesture.Steps[CurrentStep].Pose.Name;
            synTime.uid = frameCount;
            GestureStatistics.synthTimes.Add(synTime);
            frameCount++;
        }


        public ExecutionStep GetCurrentStep()
        {
            return this.Gesture.Steps[CurrentStep];
        }

        private double AccumulatedError;

        private int CompletedCount;


        public double StepLastPercentage { get; private set; }

        public int CurrentStep { get; private set; }

        public Dictionary<JointType, Point3D> LastDistanceVectors { get; set; }

        public Z3Target Target { get; private set; }
        private void Reset(Z3Body body)
        {
            this.CurrentStep = 0;
            //this.StepLastPercentage = 0;
            this.UpdateTargetBody(body);
            this.AccumulatedError = 0;
            this.Target = null;
        }

        public GestureStatus GetStatus()
        {
            GestureStatus result = new GestureStatus();

            result.Name = this.Gesture.Name;
            result.MainInstruction = this.MainRestriction;

            //result.StepNamesAndDescriptions = new List<Tuple<string, string>>(this.Gesture.Steps.Capacity);

            //foreach (var step in this.Gesture.Steps)
            //    result.StepNamesAndDescriptions.Add(new Tuple<string, string>(
            //        this.GetCurrentStep().Pose.Name,
            //        this.GetCurrentStep().Pose.ToString()));
            
            result.StepPercentage = this.StepLastPercentage;
            result.DistanceVectors = this.LastDistanceVectors;
            result.CompletedCount = this.CompletedCount;
            result.CurrentStep = this.CurrentStep;
            result.NumSteps = this.Gesture.Steps.Count;
            result.AccumulatedError = this.AccumulatedError;

            return result;
        }

        private Dictionary<JointType, Point3D> UpdateDistanceVectors(Z3Body body, Context localContext)
        {
            var distancesZ3Point3Ds = body.GrabDistancePoint3Ds(this.Target.Body, this.Target.GetAllJointTypes());
            var distancesVector3Ds = new Dictionary<JointType, Point3D>();

            foreach (var pointZ3 in distancesZ3Point3Ds)
            {
                distancesVector3Ds.Add(
                    pointZ3.Key,
                    new Point3D(
                        Z3Math.GetRealValue(pointZ3.Value.X),
                        Z3Math.GetRealValue(pointZ3.Value.Y),
                        Z3Math.GetRealValue(pointZ3.Value.Z)));
            }

            return distancesVector3Ds;
        }

        // Filters input distances, considering only a set of jointTypes
        private double CalcMaxDistance(
            Dictionary<JointType, Point3D> allDistancesPoint3Ds,
            List<JointType> selectedJoints)
        {
            var distances = new Dictionary<JointType, double>();
            var result = 0.0;

            foreach (var distancePoint3D in allDistancesPoint3Ds)
            {
                if (selectedJoints.Contains(distancePoint3D.Key))
                    distances.Add(distancePoint3D.Key, distancePoint3D.Value.Norm());
            }

            if (distances.Count > 0)
                result = distances.Values.Max();

            return result;
        }

        public string MainRestriction { get; set; }
    }


    public class Z3Target
    {
        public Z3Target()
        {
            this.Body = new Z3Body();
            this.TransformedJoints = new List<JointType>();
            this.RestrictedJoints = new List<JointType>();
        }

        public List<JointType> GetAllJointTypes()
        {
            return TransformedJoints.Union(RestrictedJoints).ToList();
        }

        public Z3Body Body;
        public List<JointType> TransformedJoints;
        public List<JointType> RestrictedJoints;
    }

    public class GestureStatus
    {
        public GestureStatus()
        {
            Name = "";
            //StepNamesAndDescriptions = new List<Tuple<string,string>>();
            CurrentStep = 0;
            StepPercentage = 0;
            DistanceVectors = new Dictionary<JointType, Point3D>();
            CompletedCount = 0;
        }

        public double GetGesturePercentage()
        {
            var result = 0.0;

            if (Succeeded)
            {
                result = 1.0;
            }
            else
            {
                result = (double) CurrentStep /
                    NumSteps +
                    StepPercentage /
                    NumSteps;
            }

            return result;
        }

        public string Name { get; set; }

        // represents the main issue the user should solve in order to accomplish the gesture
        public string MainInstruction { get; set; }

        // TODO: this is still a work in progress, 
        // it was currently consuming too much time
        // and the description was not complete
        //public List<Tuple<string,string>> StepNamesAndDescriptions { get; set; }

        public double StepPercentage { get; set; }

        public Dictionary<JointType, Point3D> DistanceVectors { get; set; }
        public int CompletedCount { get; set; }

        public int CurrentStep { get; set; }

        public int NumSteps { get; set; }

        public bool Succeeded { get; set; }

        public bool SucceededFirstFrame { get; set; }

        public bool Broke { get; set; }

        public double AccumulatedError { get; set; }

        public float Confidence { get; set; }
    }
}
