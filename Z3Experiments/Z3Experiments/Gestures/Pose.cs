using Microsoft.Z3;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PreposeGestures
{
	public class Pose
	{
		public Pose(string name)
		{
			this.Name = name;
			this.mTransform = new CompositeBodyTransform();
			this.mRestriction = new CompositeBodyRestriction();       
            this.Delayed = new CompositeDelayedStatement();
		}

		public Pose(string name, CompositeBodyTransform transform) : this(name)
		{
			this.mTransform = transform;
		}

        public Pose(string name, CompositeBodyTransform transform, IBodyRestriction restriction) 
            : this(name, transform)
        {
            this.mRestriction = (restriction is CompositeBodyRestriction) ?
                (CompositeBodyRestriction)restriction :
                new CompositeBodyRestriction((SimpleBodyRestriction)restriction);

            // Check if restriction allows transform
            if (!this.IsTransformAcceptedByRestriction())
            {
                throw new ArgumentException("The restriction does not allow the transform.", "restriction");
            }
        }

		public Pose(
            string name, 
            CompositeBodyTransform transform, 
            IBodyRestriction restriction, 
            CompositeDelayedStatement delayed) :
            this(name, transform, restriction)
		{
            this.Delayed = delayed;
		}

        public void UpdateDelayedStatements(Z3Body startBody)
        {
            this.Delayed.Update(startBody);
        }

        public int DistinctRestrictedJointsCount
        {
            get
            {
                int retval = 0;
                retval = this.Restriction.DistinctRestrictedJointsCount;
                return retval;
            }
        }

        public int RestrictionCount
        {
            get
            {
                return this.mRestriction.GetRestrictionCount();
            }
        }

        public int NegatedRestrictionCount
        {
            get
            {
                return this.mRestriction.NumNegatedRestrictions;
            }

        }

        public int TransformCount
        {
            get
            {
                return this.mTransform.GetTransformCount();
            }
        }

		public bool IsTransformAcceptedByRestriction()
		{
			Z3Body body = Z3Body.MkZ3Const();
			Z3Body transformedBody = this.mTransform.Transform(body);
			BoolExpr expr = this.mRestriction.Evaluate(transformedBody);

			SolverCheckResult checkResult = Z3AnalysisInterface.CheckStatus(expr);

			return (checkResult.Status != Status.UNSATISFIABLE);
		}		

		public void Compose(JointType jointType, JointTransform point3DTransform)
		{
			this.mTransform = this.mTransform.Compose(jointType, point3DTransform);
		}

		public void Compose(CompositeBodyTransform newTransform)
		{
			this.mTransform = this.mTransform.Compose(newTransform);
		}

		public void Compose(IBodyRestriction newRestriction)
		{
			this.mRestriction.And(newRestriction);
		}

		public bool IsBodyAccepted(
			Z3Body input)
		{
			bool result = this.Restriction.IsBodyAccepted(input);
			return result;
		}

		public bool IsTransformedBodyAccepted(Z3Body body)
		{
			bool result = false;

			Z3Body transformedBody = this.mTransform.Transform(body);
			result = this.IsBodyAccepted(transformedBody);

			return result;
		}

		public Z3Target CalcNearestTargetBody(Z3Body startBody)
		{
			// Create binary search to look for nearest body
			int numSteps = 1;
			int degreesThreshold = 90;
			int degreesIncrement = 90;
			Z3Target target = null;

			for (int i = 0; i < numSteps; ++i)
			{
				// Ask for a witness which is within the range
				target = Z3AnalysisInterface.GenerateTarget(
					this.Transform,
                    this.Restriction,
					startBody,
					degreesThreshold);                

				// Update degrees threshold
				degreesIncrement /= 2;

				if (target != null)
				{
					degreesThreshold -= degreesIncrement;
				}
				else
				{
					degreesThreshold += degreesIncrement;
				}
			}

            // If target still null it probably means Z3 was unable to solve the restrictions
            // This way we generate a target using onlye the transforms
            if(target == null)
            {
                target = new Z3Target();
                target.Body = this.Transform.Transform(startBody);
                target.TransformedJoints = this.Transform.GetJointTypes();
            }

            // If target still null assign a new body as an error proof policy
            if(target == null)
            {
                target = new Z3Target();
                target.Body = startBody;
            }

			return target;
		}

		public List<JointType> GetAllJointTypes()
		{
            return mTransform.GetJointTypes().Union(mRestriction.GetJointTypes()).ToList();
		}

        public List<JointType> GetRestrictionJointTypes()
        {
            return this.mRestriction.GetJointTypes();
        }

        public List<JointType> GetTransformJointTypes()
        {
            return this.mTransform.GetJointTypes();
        }

        private CompositeBodyTransform mTransform;
		internal CompositeBodyTransform Transform
        { 
            get
            {
                return mTransform.Compose(Delayed.Transform);
            }
            private set
            {
                this.mTransform = value;
            }
        }

        private CompositeBodyRestriction mRestriction;
        internal CompositeBodyRestriction Restriction 
        { 
            get
            {
                return mRestriction.And(Delayed.Restriction);
            }
            private set
            {
                this.mRestriction = value;
            }
        }

        internal CompositeDelayedStatement Delayed { get; private set; }

		public string Name { get; set; }

		public override string ToString()
		{
            this.Delayed.ToString();
            var jumpTransform = this.mTransform.GetTransformCount() > 0 ? "\n" : "";
            var jumpRestriction = this.mRestriction.GetRestrictionCount() > 0 ? "\n" : "";
            var jumpDelayed = this.Delayed.Statements.Count > 0 ? "\n" : "";

            var result = string.Format(
                "{0} : " +
                jumpTransform + "{1}" +
                jumpRestriction + "{2}" +
                jumpDelayed + "{3}",
                this.Name,
                this.mTransform,
                this.mRestriction,
                this.Delayed);

            return result.ToLower();
		}

        // returns the min percentage of completion from all restrictions
        internal double CalcMinPercentage(Z3Body body)
        {            
            var percentage = this.Restriction.CalcPercentage(body);

            return percentage;
        }
    }
}
