using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Z3;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;

namespace PreposeGestures
{
    public class Validity
    {

        public static bool IsInternallyValid(
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
                if (!Validity.IsInternallyValid(gesture, out exceptions))
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

        public static bool IsInternallyValid(Gesture gesture, out List<PoseSafetyException> exceptions)
        {
            bool result = true;
            exceptions = new List<PoseSafetyException>();
            foreach (var step in gesture.Steps)
            {
                var pose = step.Pose;
                Z3Body witness = null;
                if (!Validity.IsInternallyValid(pose))
                {
                    var exception = new PoseSafetyException(
                        "Pose failed internal validity check!", pose, witness
                        );
                    exceptions.Add(exception);
                    result = false;
                }
            }

            return result;
        }

        public static bool IsInternallyValid(Pose pose)
        {
            Z3Body input = Z3Body.MkZ3Const();
            Z3Body transformed = pose.Transform.Transform(input);

            // We have to check that the pose is within the default safety restriction
            IBodyRestriction safe = Safety.DefaultSafetyRestriction();

            BoolExpr inputSafe = safe.Evaluate(input);
            BoolExpr transformedRestricted = pose.Restriction.Evaluate(transformed);

            // Try to generate a safe witness using the transform
            BoolExpr outputSafe = safe.Evaluate(transformed);

            // Check to see if the transform is not satisfiable -- if so, then it is not internally valid
            BoolExpr expr = Z3.Context.MkAnd(inputSafe, transformedRestricted, outputSafe);


            SolverCheckResult solverResult = Z3AnalysisInterface.CheckStatus(expr);

            if (solverResult.Status == Status.SATISFIABLE)
            {
                // We can create a witness - therefore the pose must be valid
                return true;
            }
            else if (solverResult.Status == Status.UNKNOWN)
            {
                return false;
            }
            else
            {
                Contract.Assert(solverResult.Status == Status.UNSATISFIABLE);
                // Pose is not internally valid and as a result there can be no witness created
                return false;
            }
        }

        public static bool IsInternallyValid(
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
                if (!Validity.IsInternallyValid(gesture, out errorMessage))
                {
                    errorMessage = "\tThe gesture " + gesture.Name + " is invalid.\n" + errorMessage;

                    result = false;
                    break;
                }
                stopwatch.Stop();
                elapsedTimes.Add(stopwatch.ElapsedMilliseconds);
            }
            return result;
        }

        public static bool IsInternallyValid(Gesture gesture, out string firstBadStep)
        {
            firstBadStep = "";
            bool result = true;
            int count = 0;
            foreach (var step in gesture.Steps)
            {
                ++count;
                var pose = step.Pose;
                Z3Body witness = null;
                if (!Validity.IsInternallyValid(pose, out firstBadStep))
                {
                    firstBadStep = "\tOn the step " + count + ": " + step.ToString() + 
                        ".\n\tOn the statement: " + firstBadStep + 
                        ".\n\tThis the first found statement that makes the gesture invalid.\n"; 

                    result = false;
                    break;
                }
            }

            return result;
        }

        public static bool IsInternallyValid(Pose pose, out string firstBadStatement)
        {
            bool result = true;
            firstBadStatement = "";
            Z3Body input = Z3Body.MkZ3Const();
            Z3Body transformed = pose.Transform.Transform(input);
            var restrictions = pose.Restriction.Restrictions;

            var composite = new CompositeBodyRestriction();

            foreach (var restriction in restrictions)
            {
                composite.And(restriction);
                BoolExpr transformedRestricted = composite.Evaluate(transformed);
                SolverCheckResult solverResult = Z3AnalysisInterface.CheckStatus(transformedRestricted);

                if(solverResult.Status == Status.UNSATISFIABLE)
                {
                    firstBadStatement = restriction.ToString();
                    result = false;
                    break;
                }
            }

            return result;
        }
    }
}
