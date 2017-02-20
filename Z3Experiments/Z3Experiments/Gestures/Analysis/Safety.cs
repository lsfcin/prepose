using Microsoft.Z3;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;

namespace PreposeGestures
{
    /// <summary>
    /// Various safety checks.
    /// </summary>
    public class Safety
    {

        //BoolExpr expr = Z3.Context.MkNot(Z3.Context.MkAnd(
        //            Z3.Context.MkLt(body.Joints[JointType.ElbowRight].Z, body.Joints[JointType.ShoulderRight].Z),
        //            Z3.Context.MkGt(body.Joints[JointType.ElbowRight].Y, body.Joints[JointType.ShoulderRight].Y)));
        //        return expr;
        //    }, body => { return 1.0; },
        //    "don't put your right elbow behind you and above your shoulders.");

        public static bool IsWithinDefaultSafetyRestrictions(
            App app,
            out string errorMessage,
            out List<long> elapsedTimes)
        {
            errorMessage = "";
            elapsedTimes = new List<long>();
            var result = true;
            foreach (var gesture in app.Gestures)
            {
                var stopwatch = new Stopwatch();
                stopwatch.Start();
                if (!Safety.IsWithinDefaultSafetyRestrictions(gesture, out errorMessage))
                {
                    errorMessage = "\tThe gesture " + gesture.Name + " is unsafe.\n" + errorMessage;

                    result = false;
                    break;
                }
                stopwatch.Stop();
                elapsedTimes.Add(stopwatch.ElapsedMilliseconds);
            }
            return result;
        }

        public static bool IsWithinDefaultSafetyRestrictions(Gesture gesture, out string firstBadStep)
        {
            firstBadStep = "";
            bool result = true;
            foreach (var pose in gesture.DeclaredPoses)
            {
                Z3Body witness = null;
                if (!Safety.IsWithinDefaultSafetyRestrictions(pose, out firstBadStep))
                {
                    firstBadStep = "\tOn the pose: " + pose.Name +
                        ".\n\tBecause it inflicts the following statement:\n\t" + firstBadStep;
                    result = false;
                    break;
                }
            }

            return result;
        }

        public static bool IsWithinDefaultSafetyRestrictions(Pose pose, out string firstBadStatement)
        {
            bool result = true;
            firstBadStatement = "";

            Z3Body input = Z3Body.MkZ3Const();
            Z3Body transformed = pose.Transform.Transform(input);
            BoolExpr transformedRestricted = pose.Restriction.Evaluate(transformed);

            var restrictions = Safety.DefaultSafetyRestriction().Restrictions;
            var composite = new CompositeBodyRestriction();

            foreach (var restriction in restrictions)
            {
                composite.And(restriction);

                BoolExpr inputSafe = composite.Evaluate(transformed);
                BoolExpr expr = Z3.Context.MkAnd(transformedRestricted, inputSafe);
                SolverCheckResult solverResult = Z3AnalysisInterface.CheckStatus(expr);

                if (solverResult.Status == Status.UNSATISFIABLE)
                {
                    firstBadStatement = ((SimpleBodyRestriction)restriction).Message;
                    result = false;
                    break;
                }
            }
            return result;
        }

        public static bool IsWithinDefaultSafetyRestrictions(
            App app,
            out List<PoseSafetyException> allExceptions, 
            out List<long> elapsedTimes)
        {
            allExceptions = new List<PoseSafetyException>();
            elapsedTimes = new List<long>();
            var result = true;
            foreach (var gesture in app.Gestures)
            {
                List<PoseSafetyException> exceptions = null;
                var stopwatch = new Stopwatch();
                stopwatch.Start();
                if (!IsWithinDefaultSafetyRestrictions(gesture, out exceptions))
                {
                    result = false;
                    Contract.Assert(exceptions != null);
                    allExceptions.AddRange(exceptions);
                }
                stopwatch.Stop();
                elapsedTimes.Add(stopwatch.ElapsedMilliseconds);
            }
            return result;
        }

        /// <summary>
        /// Checks if the pose is within default safety 
        /// restrictions when the transform and restrictions 
        /// are applied.
        /// </summary>
        /// <returns>True if it's safe</returns>
        public static bool IsWithinDefaultSafetyRestrictions(
            Gesture gesture, 
            out List<PoseSafetyException> exceptions)
        {
            bool result = true;
            exceptions = new List<PoseSafetyException>();
            foreach (var step in gesture.Steps)
            {
                var pose = step.Pose;
                Z3Body witness = null;
                if (!Safety.IsWithinSafetyRestrictions(pose, out witness))
                {
                    var exception = new PoseSafetyException(
                        "Default safety violation", pose, witness
                        );
                    exceptions.Add(exception);
                    result = false;
                }
            }

            return result;
        }


        /// <summary>
        /// Checks if the pose is within default safety 
        /// restrictions when the transform and restrictions 
        /// are applied.
        /// </summary>
        /// <returns>True if it's safe</returns>
        public static bool IsWithinSafetyRestrictions(Pose pose, out Z3Body witness)
        {
            Z3Body input = Z3Body.MkZ3Const();
            Z3Body transformed = pose.Transform.Transform(input);

            IBodyRestriction safe = Safety.DefaultSafetyRestriction();

            BoolExpr inputSafe = safe.Evaluate(input);
            BoolExpr transformedRestricted = pose.Restriction.Evaluate(transformed);

            // Try to generate a unsafe witness using the transform
            BoolExpr outputUnsafe = Z3.Context.MkNot(safe.Evaluate(transformed));

            // Put together all expressions and search for unsat
            BoolExpr expr = Z3.Context.MkAnd(inputSafe, transformedRestricted, outputUnsafe);

            SolverCheckResult solverResult = Z3AnalysisInterface.CheckStatus(expr);

            if (solverResult.Status == Status.SATISFIABLE)
            {
                //Z3Body 
                witness =
                    Z3AnalysisInterface.CreateBodyWitness(
                    transformed,
                    solverResult.Model,
                    pose.GetAllJointTypes(),
                    JointTypeHelper.CreateDefaultZ3Body());

                return false;
            }
            else if (solverResult.Status == Status.UNKNOWN)
            {
                //Z3Body 
                witness = JointTypeHelper.CreateDefaultZ3Body();

                return false;
            }
            else
            {
                Contract.Assert(solverResult.Status == Status.UNSATISFIABLE);
                witness = null;
                return true;
            }
        }


        /// <summary>
        /// This is a basic safety check to make sure we don't break any bones.
        /// </summary>
        /// <returns>True if safe</returns>
        public static CompositeBodyRestriction DefaultSafetyRestriction()
        {
            var result = new CompositeBodyRestriction();

            //int inclinationThreshold = 45;

            //// Head
            //// Make sure neck is not inclinated beyond the threshold            
            //var head = new SimpleBodyRestriction(body =>
            //{
            //    Z3Point3D up = new Z3Point3D(0, 1, 0);
            //    BoolExpr expr1 = body.Joints[JointType.Head].IsDegreesBetweenLessThan(up, inclinationThreshold);
            //    BoolExpr expr2 = body.Joints[JointType.Neck].IsDegreesBetweenLessThan(up, inclinationThreshold);
            //    BoolExpr expr = Z3.Context.MkAnd(expr1, expr2);

            //    return expr;
            //},
            //body => { return 1.0; });
            //result.And(head);

            //// Spine
            //// Make sure spine is not inclinated beyond the threshold
            //var spine = new SimpleBodyRestriction(body =>
            //{
            //    Z3Point3D up = new Z3Point3D(0, 1, 0);
            //    BoolExpr expr1 = body.Joints[JointType.SpineMid].IsDegreesBetweenLessThan(up, inclinationThreshold);
            //    BoolExpr expr2 = body.Joints[JointType.SpineShoulder].IsDegreesBetweenLessThan(up, inclinationThreshold);
            //    BoolExpr expr3 =
            //        body.Joints[JointType.SpineMid].IsDegreesBetweenLessThan(
            //        body.Joints[JointType.SpineShoulder], inclinationThreshold);
            //    BoolExpr expr = Z3.Context.MkAnd(expr1, expr2, expr3);

            //    return expr;
            //},
            //body => { return 1.0; });
            //result.And(spine);

            //// Shoulders
            //// Make sure shoulders are not bent            
            //var shoulders = new SimpleBodyRestriction(body =>
            //{
            //    BoolExpr expr =
            //        body.Joints[JointType.SpineMid].IsDegreesBetweenGreaterThan(
            //        body.Joints[JointType.SpineShoulder], 120);
            //    return expr;
            //},
            //body => { return 1.0; });
            //result.And(shoulders);

            // Elbows
            // Make sure elbows are not behind the back
            // And also that they are not on the top/back sub-space
            var elbows1 = new SimpleBodyRestriction(body =>
            {
                var joint1Position = body.GetJointZ3Position(JointType.ElbowRight);
                var joint2Position = body.GetJointZ3Position(JointType.ShoulderRight);
                BoolExpr expr = Z3.Context.MkNot(Z3.Context.MkAnd(
                    Z3.Context.MkLt(joint1Position.Z, joint2Position.Z),
                    Z3.Context.MkGt(joint1Position.Y, joint2Position.Y)));
                return expr;
            }, body => { return 1.0; },
            "don't put your right elbow behind you and above your shoulders.\n");
            var elbows2 = new SimpleBodyRestriction(body =>
            {
                var joint1Position = body.GetJointZ3Position(JointType.ElbowLeft);
                var joint2Position = body.GetJointZ3Position(JointType.ShoulderLeft);
                BoolExpr expr = Z3.Context.MkNot(Z3.Context.MkAnd(
                    Z3.Context.MkLt(joint1Position.Z, joint2Position.Z),
                    Z3.Context.MkGt(joint1Position.Y, joint2Position.Y)));
                return expr;
            }, body => { return 1.0; },
            "don't put your left elbow behind you and above your shoulders.\n");
            var elbows3 = new SimpleBodyRestriction(body =>
            {
                var joint1Position = body.GetJointZ3Position(JointType.ElbowRight);
                var joint2Position = body.GetJointZ3Position(JointType.ShoulderRight);
                BoolExpr expr = Z3.Context.MkNot(Z3.Context.MkAnd(
                    Z3.Context.MkLt(joint1Position.Z, joint2Position.Z),
                    Z3.Context.MkLt(joint1Position.X, joint2Position.X)));
                return expr;
            }, body => { return 1.0; },
            "don't put your right elbow behind you crossing your back.\n");
            var elbows4 = new SimpleBodyRestriction(body =>
            {
                var joint1Position = body.GetJointZ3Position(JointType.ElbowLeft);
                var joint2Position = body.GetJointZ3Position(JointType.ShoulderLeft);
                BoolExpr expr = Z3.Context.MkNot(Z3.Context.MkAnd(
                    Z3.Context.MkLt(joint1Position.Z, joint2Position.Z),
                    Z3.Context.MkGt(joint1Position.X, joint2Position.X)));
                return expr;
            }, body => { return 1.0; },
            "don't put your right elbow behind you crossing your back.\n");

            result.And(elbows1);
            result.And(elbows2);
            result.And(elbows3);
            result.And(elbows4);

            //// Wrists
            //// Make sure the inclination of wrists towards the back is not higher than the inclinatin of the elbows
            //// unless elbows are up or wrists are directed to torso
            //// TODO

            //// Hips
            //// Make sure hips are aligned with the shoulders or at lest within the range
            //var hips = new SimpleBodyRestriction(body =>
            //{
            //    Z3Point3D shouldersSum =
            //        body.Joints[JointType.ShoulderLeft].GetInverted() +
            //        body.Joints[JointType.ShoulderRight];

            //    Z3Point3D hipsSum =
            //        body.Joints[JointType.HipLeft].GetInverted() +
            //        body.Joints[JointType.HipRight];

            //    BoolExpr expr = shouldersSum.IsDegreesBetweenLessThan(hipsSum, 45);
            //    return expr;
            //},
            //body => { return 1.0; });
            //result.And(hips);

            // Legs
            // Make sure knees do not bent towards your back
            var knees1 = new SimpleBodyRestriction(body =>
            {
                var joint1Position = body.GetJointZ3Position(JointType.KneeLeft);
                var joint2Position = body.GetJointZ3Position(JointType.HipLeft);
                var joint3Position = body.GetJointZ3Position(JointType.AnkleLeft);
                BoolExpr expr = Z3.Context.MkNot(Z3.Context.MkAnd(
                    Z3.Context.MkLt(joint1Position.Z, joint2Position.Z),
                    Z3.Context.MkLt(joint1Position.Z, joint3Position.Z)));
                return expr;
            }, body => { return 1.0; },
            "don't bend your left knee towards your back.\n");
            var knees2 = new SimpleBodyRestriction(body =>
            {
                var joint1Position = body.GetJointZ3Position(JointType.KneeRight);
                var joint2Position = body.GetJointZ3Position(JointType.HipRight);
                var joint3Position = body.GetJointZ3Position(JointType.AnkleRight);
                BoolExpr expr = Z3.Context.MkNot(Z3.Context.MkAnd(
                    Z3.Context.MkLt(joint1Position.Z, joint2Position.Z),
                    Z3.Context.MkLt(joint1Position.Z, joint3Position.Z)));
                return expr;
            }, body => { return 1.0; },
            "don't bend your right knee towards your back.\n");

            result.And(knees1);
            result.And(knees2);

            // Ankles
            // Make sure ankles are not inclinated up more than knees
            // unless ankles are pointing back
            // TODO

            return result;
        }
    }
}
