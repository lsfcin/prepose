using Microsoft.Z3;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace PreposeGestures
{
	public class Ambiguity
	{
	//	public IList<BodyTransform> Transforms { get; private set; }

	//	public Ambiguity(App app)
	//	{
	//		this.Transforms = GetTransforms(app);
	//	}

	//	private Ambiguity(IList<BodyTransform> transforms, IList<string> names = null)
	//	{
	//		Contract.Requires(names == null || names.Count == transforms.Count);
	//		this.Transforms = new List<BodyTransform>(transforms);
	//	}

	//	private static IList<BodyTransform> GetTransforms(App app)
	//	{
	//		throw new NotImplementedException();
	//	}

	//	/// <summary>
	//	/// Checks if the set has duplicate transforms.
	//	/// </summary>
	//	/// <returns></returns>
	//	public bool HasDuplicates(IList<string> names = null)
	//	{
	//		for (int i = 0; i < this.Transforms.Count; ++i)
	//		{
	//			// these are ordered pairs of transforms (i,j) where i<j
	//			for (int j = i + 1; j < Transforms.Count; ++j)
	//			{
	//				if (Transforms[i].Equals(Transforms[j]))
	//				{
	//					Console.WriteLine("Transforms {0} and {1} are equals.", 
	//						i, j);
	//					Console.WriteLine("Transform {0}",
	//						names != null ? names[i] : Transforms[i].ToString());
	//					Console.WriteLine("Transform {0}",
	//						names != null ? names[j] : Transforms[j].ToString());

	//					return true;
	//				}
	//			}
	//		}

	//		return false;
	//	}

	//	private IEnumerable<KeyValuePair<string, BodyTransform>> GetPairs() {
	//		for (int i = 0; i < this.Transforms.Count; ++i)
	//		{
	//			// these are ordered pairs of transforms (i,j) where i<j
	//			for (int j = i + 1; j < Transforms.Count; ++j)
	//			{
	//				var result = new KeyValuePair<string,BodyTransform>
	//					(
	//						string.Format("({0},{1})",i,j), 
	//						Transforms[i].Compose(Transforms[j])
	//					);
	//				yield return result;
	//			}
	//		}
	//	}

	//	public bool HasConflictingPairs()
	//	{
	//		var pairs = GetPairs();
	//		IList<BodyTransform> transforms = pairs.Select(p => p.Value).ToList();
	//		IList<string> names = pairs.Select(p => p.Key).ToList();
	//		var pairAmbiguity = new Ambiguity(transforms, names);
	//		return pairAmbiguity.HasDuplicates();
	//	}


        public class AmbiguityTime
        {
            public Gesture Gesture1 { get; internal set; }
            public Gesture Gesture2 { get; internal set; }

            public long Time { get; internal set; }

            public bool Conflict { get; internal set; }

            public SolverCheckResult CheckResult { get; internal set; }
        }

        public static bool DumpZ3Constraints = true; 

        /// <summary>
        /// Check for pairwise ambiguity
        /// </summary>
        /// <returns></returns>
        public static bool HasPairwiseConflicts(
            App app, 
            out string errorMessage, 
            out List<AmbiguityTime> ambiguityTimes, 
            int precision = 15)
        {
            errorMessage = "";
            List<Gesture> gestures = (List<Gesture>)app.Gestures;

            bool result = false;
            ambiguityTimes = new List<AmbiguityTime>();

            for (int i = 0; i < gestures.Count - 1; i++)
            {
                for (int j = i + 1; j < gestures.Count; j++)
                {
                    var gesture1 = gestures[i];
                    var gesture2 = gestures[j];

                    var gesture1CurrentStep = 0;
                    var gesture2CurrentStep = 0;
                    var gesture1LastConflictedStep = 0;
                    var gesture2LastConflictedStep = 0;
                    var gesture1NumSteps = gesture1.Steps.Count;
                    var gesture2NumSteps = gesture2.Steps.Count;

                    var advanceOn = 1;
                    var conflictCount = 0;

                    while (gesture1CurrentStep < gesture1NumSteps && gesture2CurrentStep < gesture2NumSteps)
                    {
                        var expr1 = ExpressionFromCurrentStep(gesture1, gesture1CurrentStep);
                        var expr2 = ExpressionFromCurrentStep(gesture2, gesture2CurrentStep);
                        var expr = Z3.Context.MkAnd(expr1, expr2);
                        var solver = Z3AnalysisInterface.CheckStatus(expr);

                        if (solver.Status == Status.SATISFIABLE)
                        {
                            gesture1LastConflictedStep = gesture1CurrentStep;
                            gesture2LastConflictedStep = gesture2CurrentStep;
                            ++gesture1CurrentStep;
                            ++gesture2CurrentStep;
                            ++conflictCount;
                            advanceOn = 1;
                        }
                        else if (solver.Status == Status.UNSATISFIABLE)
                        {
                            if (gesture1CurrentStep == gesture1NumSteps - 1)
                            {
                                gesture1CurrentStep = gesture1LastConflictedStep;
                                advanceOn = 2;
                            }
                            else if (gesture2CurrentStep == gesture2NumSteps - 1)
                            {
                                conflictCount = 0;
                            }

                            if (advanceOn == 1)
                                ++gesture1CurrentStep;
                            else if (advanceOn == 2)
                                ++gesture2CurrentStep;
                        }
                    }
                    var conflicted = (conflictCount > 0);
                    if(conflicted)
                    {
                        result = true;

                        var full = false;
                        if(conflictCount >= Math.Min(gesture1NumSteps, gesture2NumSteps))
                            full = true;
                        if(full)
                        {
                            errorMessage += "\tFull conflict found between gestures " + gesture1.Name + " and " + gesture2.Name + ",\n";
                            errorMessage += "\twhich means that one gesture may be fully executed whithin the other.\n";
                        }
                        else
                        {
                            errorMessage += "\tPartial conflict found between gestures " + gesture1.Name + " and " + gesture2.Name + ",\n";
                            errorMessage += "\twhich means that one gesture may be started before the other one ended.\n";
                            errorMessage += "\tIn total " + conflictCount + " sequential steps conflicted between those gestures.\n";
                        }
                    }
                }
            }           

            return result;
        }

        private static BoolExpr ExpressionFromCurrentStep(Gesture gesture1, int gesture1CurrentStep)
        {
            var input1 = Z3Body.MkZ3Const();
            var step1 = gesture1.Steps[gesture1CurrentStep];
            var pose1 = step1.Pose;
            input1 = pose1.Transform.Transform(input1);
            var expr1 = pose1.Restriction.Evaluate(input1);
            return expr1;
        }
        
        public static bool HasPairwiseConflicts(App app, out List<PairwiseConflictException> allExceptions, out List<AmbiguityTime> ambiguityTimes, int precision = 15)
        {
            List<Gesture> conflictGestures = (List<Gesture>)app.Gestures;

            bool result = false;
            allExceptions = new List<PairwiseConflictException>();
            ambiguityTimes = new List<AmbiguityTime>();

            for (int i = 0; i < conflictGestures.Count - 1; i++)
            {
                for (int j = i + 1; j < conflictGestures.Count; j++)
                {
                    var gesture1 = conflictGestures[i];
                    var gesture2 = conflictGestures[j];

                    // Create const input body
                    var input = Z3Body.MkZ3Const();

                    var allJoints = EnumUtil.GetValues<JointType>().ToList();

                    Z3Body transformed1 = null;
                    Z3Body transformed2 = null;

                    BoolExpr evaluation1 = Z3Math.True;
                    BoolExpr evaluation2 = Z3Math.True;

                    // Pass input through both gestures
                    gesture1.FinalResult(input, out transformed1, out evaluation1);
                    gesture2.FinalResult(input, out transformed2, out evaluation2);

                    // Check if it is possible that both outputs are equals
                    // This is performed by checking if is possible that all expressions are true

                    var isNearExpr = transformed1.IsNearerThan(
                    transformed2, precision);
                    var expr = Z3.Context.MkAnd(isNearExpr, evaluation1, evaluation2);

                    // If we are dumping Z3 constraints, then convert the expression to a SMTLIB formatted string
                    // and dump it to disk. Note this is not included in the timing for the individual pair of gestures,
                    // but it _is_ included in the timing for the app overall. 
                    if (DumpZ3Constraints)
                    {
                        string exprName = String.Join("X", gesture1.Name, gesture2.Name);
                        string exprPath = exprName + ".smt2";
                        Z3AnalysisInterface.WriteExprToDisk(expr, exprName, exprPath);
                    }

                    // Check if we have an ambiguity conflict. Record the time it takes. 
                    var stopwatch = new Stopwatch();
                    stopwatch.Start();
                    var checkResult = Z3AnalysisInterface.CheckStatus(expr);
                    stopwatch.Stop();


                    if (checkResult.Status == Status.SATISFIABLE)
                    {
                        var witness = Z3AnalysisInterface.CreateBodyWitness(
                            input,
                            checkResult.Model,
                            allJoints,
                            JointTypeHelper.CreateDefaultZ3Body());

                        var exception = new PairwiseConflictException(
                            "Conflict detected between pair of gestures",
                            gesture1,
                            gesture2,
                            witness);

                        allExceptions.Add(exception);

                        result = true;
                    }
                    // TODO the witness here should exist, this case shouldn't be needed
                    else if (checkResult.Status == Status.UNKNOWN)
                    {
                        var witness = JointTypeHelper.CreateDefaultZ3Body();

                        var exception = new PairwiseConflictException(
                            "Conflict detected between pair of gestures, the reason is unknown",
                            gesture1,
                            gesture2,
                            witness);

                        allExceptions.Add(exception);

                        result = true;
                    }

                    ambiguityTimes.Add(new AmbiguityTime
                    {
                        Gesture1 = gesture1,
                        Gesture2 = gesture2,
                        Time = stopwatch.ElapsedMilliseconds,
                        Conflict = result,
                        CheckResult = checkResult
                    });


                }
            }

            return result;
        }
	}
    public class PairwiseConflictException : Exception
    {
        public Z3Body Witness { get; private set; }
        public Gesture Gesture1 { get; private set; }
        public Gesture Gesture2 { get; private set; }
        public PairwiseConflictException(string message) : base(message) { }
        public PairwiseConflictException(string message, Gesture gesture1, Gesture gesture2, Z3Body body)
            : base(message)
        {
            this.Witness = body;
            this.Gesture1 = gesture1;
            this.Gesture2 = gesture2;
        }

        public override string ToString()
        {
            return string.Format("Gesture 1: {0}, gesture 2: {1}, witness: {2}",
                this.Gesture1.Name, this.Gesture2.Name, this.Witness);
        }
    }
}
