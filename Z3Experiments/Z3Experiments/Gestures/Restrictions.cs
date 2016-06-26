using Microsoft.Z3;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq.Expressions;
using System.Reflection;

namespace PreposeGestures
{
    public interface IBodyRestriction
    {
        bool IsBodyAccepted(Z3Body body);

        BoolExpr Evaluate(Z3Body body);

        // Keep track of which joints are restricted
        List<JointType> GetJointTypes();

        int GetRestrictionCount();
    }

    public class CompositeBodyRestriction : IBodyRestriction
    {
        // Create an empty body restriction
        public CompositeBodyRestriction()
        {
            this.Restrictions = new List<SimpleBodyRestriction>();
        }

        public int DistinctRestrictedJointsCount
        {
            get
            {
                int retval = 0;
                List<JointType> allJointsRestricted = new List<JointType>();
                foreach (var r in this.Restrictions)
                {
                    foreach (var j in r.GetJointTypes())
                    {
                        allJointsRestricted.Add(j);
                    }
                }
                var distinctAllJointsRestricted = allJointsRestricted.Distinct();
                retval = distinctAllJointsRestricted.Count(); 

                return retval; 
            }
        }

        public CompositeBodyRestriction(SimpleBodyRestriction soleBodyRestriction)
        {
            this.Restrictions = new List<SimpleBodyRestriction>();
            this.Restrictions.Add(soleBodyRestriction);
        }

        public int NumNegatedRestrictions
        {
            get
            {
                int retval = 0;
                foreach (var r in this.Restrictions)
                {
                    if (r.isNegated)
                    {
                        retval++; 
                    }
                }
                return retval; 
            }
        }

        public void And(SimpleBodyRestriction that)
        {
            this.Restrictions.Add(that);
        }

        public CompositeBodyRestriction And(IBodyRestriction that)
        {
            CompositeBodyRestriction result = new CompositeBodyRestriction();

            result.Restrictions.AddRange(this.Restrictions);
            if (that is CompositeBodyRestriction)
            {
                result.Restrictions.AddRange(((CompositeBodyRestriction)that).Restrictions);
            }
            else
            {
                result.Restrictions.Add(((SimpleBodyRestriction)that));
            }

            return result;
        }

        public BoolExpr Evaluate(Z3Body body)
        {
            Func<Z3Body, BoolExpr> composedExpr = bodyInput =>
            {
                BoolExpr result = Z3.Context.MkTrue();

                foreach (var restriction in this.Restrictions)
                {
                    result = Z3.Context.MkAnd(result, restriction.Evaluate(bodyInput));
                }

                return result;
            };

            return composedExpr(body);
        }

        public bool IsBodyAccepted(Z3Body body)
        {
            return IsBodyAccepted(body, Z3.Context);
        }
        public bool IsBodyAccepted(Z3Body body, Context localContext)
        {
            BoolExpr resultExpr = Evaluate(body);
            bool result = Z3.EvaluateBoolExpr(resultExpr, localContext);
            
            return result;
        }

        public double CalcPercentage(Z3Body body)
        {
            var result = 1.0;

            foreach (var restriction in this.Restrictions)
            {
                result = Math.Min(result, restriction.Percentage(body));
            }

            return result;
        }

        protected List<SimpleBodyRestriction> Restrictions;

        
        public List<JointType> GetJointTypes()
        {
            List<JointType> result = new List<JointType>();

            foreach(var restriction in Restrictions)
            {
                List<JointType> jointTypes = restriction.GetJointTypes();
                foreach (var jointType in jointTypes)
                {
                    if (!result.Contains(jointType))
                        result.Add(jointType);
                }
            }

            return result;
        }

        public override string ToString()
        {
            var result = "";
            if (Restrictions.Count > 0)
            {
                var count = 0;
                foreach(var restriction in this.Restrictions)
                {
                    if (count > 0)
                        result += "\n";

                    result += restriction.ToString();
                    count++;
                }
            }

            return result;
        }

        public int GetRestrictionCount()
        {
            return this.Restrictions.Count;
        }
    }

    public class SimpleBodyRestriction : IBodyRestriction
    {
        public bool isNegated; 
        // restrictedJoints is the list of the the joints that must be activated by the restriction
        internal SimpleBodyRestriction(
            Func<Z3Body, BoolExpr> restriction,
            Func<Z3Body, double> percentage, 
            List<JointType> restrictedJoints)
        {
            this.RestrictionFunc = restriction;
            this.PercentageFunc = percentage;
            this.RestrictedJoints = restrictedJoints;
        }

        internal SimpleBodyRestriction(
            Func<Z3Body, BoolExpr> restriction,
            Func<Z3Body, double> percentage,
            JointType restrictedJoint)
        {
            this.RestrictionFunc = restriction;
            this.PercentageFunc = percentage;
            this.RestrictedJoints = new List<JointType>();
            this.RestrictedJoints.Add(restrictedJoint);
        }

        internal SimpleBodyRestriction(
            Func<Z3Body, BoolExpr> restriction,
            Func<Z3Body, double> percentage,
            params JointType[] restrictedJoints)
        {
            Contract.Ensures(restrictedJoints.Length > 0);

            this.RestrictionFunc = restriction;
            this.PercentageFunc = percentage;
            this.RestrictedJoints = new List<JointType>();

            foreach(var restrictedJoint in restrictedJoints)
                this.RestrictedJoints.Add(restrictedJoint);
        }

        public override bool Equals(object obj)
        {
            var that = obj as SimpleBodyRestriction;
            Solver solver = Z3.Context.MkSolver();
            
            Z3Body body = Z3Body.MkZ3Const();
            var jointTypes = EnumUtil.GetValues<JointType>();
            BoolExpr thisResult = this.Evaluate(body);
            BoolExpr thatResult = that.Evaluate(body);

            BoolExpr equalsExpr = Z3.Context.MkEq(thisResult, thatResult);

            solver.Assert(Z3.Context.MkNot(equalsExpr));
            Status status = solver.Check();
            Statistics stats = solver.Statistics; 

            //Console.WriteLine("EqualsExpr: " + equalsExpr);
            //Console.WriteLine("Proving: " + equalsExpr);
            //switch (status)
            //{
            //    case Status.UNKNOWN:
            //        Console.WriteLine("Unknown because:\n" + solver.ReasonUnknown);
            //        break;
            //    case Status.SATISFIABLE:
            //        throw new ArgumentException("Test Failed Expception");
            //    case Status.UNSATISFIABLE:
            //        Console.WriteLine("OK, proof:\n" + solver.Proof);
            //        break;
            //}

            bool result = false;
            if (status == Status.UNSATISFIABLE)
                result = true;
            
            return result;
        }

        public bool IsBodyAccepted(Z3Body body)
        {
            BoolExpr resultExpr = RestrictionFunc(body);

            Solver solver = Z3.Context.MkSolver();

            solver.Push();
            solver.Assert(Z3.Context.MkNot(resultExpr));
            Status status = solver.Check();
            Statistics stats = solver.Statistics; 

            bool result = (status == Status.UNSATISFIABLE);

            solver.Pop();

            return result;
        }

        public BoolExpr Evaluate(Z3Body body)
        {
            return this.RestrictionFunc(body);
        }

        public double Percentage(Z3Body body)
        {
            return this.PercentageFunc(body);
        }

        public List<JointType> GetJointTypes()
        {
            return this.RestrictedJoints;
        }

        static object ToStringHelper(System.Linq.Expressions.Expression exp)
        {
            switch (exp.NodeType)
            {
                case ExpressionType.Constant:
                    return ((ConstantExpression)exp).Value;
                case ExpressionType.MemberAccess:
                    var me = (MemberExpression)exp;
                    switch (me.Member.MemberType)
                    {
                        case System.Reflection.MemberTypes.Field:
                            return ((FieldInfo)me.Member).GetValue(ToStringHelper(me.Expression));
                        case MemberTypes.Property:
                            return ((PropertyInfo)me.Member).GetValue(ToStringHelper(me.Expression), null);
                        default:
                            throw new NotSupportedException(me.Member.MemberType.ToString());
                    }
                default:
                    throw new NotSupportedException(exp.NodeType.ToString());
            }
        }

        // this returns the boolean expression which tells if the input body met the restriction requirements 
        private Func<Z3Body, BoolExpr> RestrictionFunc { get; set; }

        // this returns how near to complete the restrictions is the current body, in a scale from 0 to 1
        private Func<Z3Body, double> PercentageFunc { get; set; }

        // Keep track of which joints are restricted
        private List<JointType> RestrictedJoints;


        public int GetRestrictionCount()
        {
            return 1;
        }
    }

    public class NoBodyRestriction : SimpleBodyRestriction
    {
        public NoBodyRestriction() : base(
            body => Z3.Context.MkTrue(),
            body => 1.0, 
            new List<JointType>()
            ) { }

        public override string ToString()
        {
            return "none";
        }
    }
    
    public class TouchBodyRestriction : SimpleBodyRestriction
    {
        public TouchBodyRestriction(JointType jointType, JointSide handSide, double distanceThreshold = 0.2, bool dont = false)
            : base(body =>
            {
                JointType sidedHandType = JointTypeHelper.GetSidedJointType(SidedJointName.Hand, handSide);

                Z3Point3D joint1Position = body.GetJointPosition(jointType);
                Z3Point3D joint2Position = body.GetJointPosition(sidedHandType);

                BoolExpr expr = joint1Position.IsNearerThan(joint2Position, distanceThreshold);
                if (dont)
                {
                    expr = Z3.Context.MkNot(expr);
                }

                return expr;
            },
            body =>
            {
                var sidedHandType = JointTypeHelper.GetSidedJointType(SidedJointName.Hand, handSide);

                var joint1Position = body.GetJointPosition(jointType);
                var joint2Position = body.GetJointPosition(sidedHandType);

                var p1 = new Point3D(
                    joint1Position.GetXValue(), 
                    joint1Position.GetYValue(), 
                    joint1Position.GetZValue());

                var p2 = new Point3D(
                    joint2Position.GetXValue(),
                    joint2Position.GetYValue(),
                    joint2Position.GetZValue());

                var distance = Math.Max(0.00000001, p1.DistanceTo(p2));
                var percentage = Math.Min(1.0, distanceThreshold / distance);

                if (dont)
                    percentage = Math.Min(1.0, distance / distanceThreshold);

                return percentage;
            },
            jointType,
            JointTypeHelper.GetSidedJointType(SidedJointName.Hand, handSide))
        {
            this.JointType = jointType;
            this.HandSide = handSide;
            if (dont)
            {
                isNegated = true; 
            }
            else
            {
                isNegated = false;
            }
        }

        public override string ToString()
        {
            return string.Format("touch your {0} with your {1} hand", 
                EnumUtil.GetDescription<JointType>(this.JointType), 
                EnumUtil.GetDescription<JointSide>(this.HandSide));
        }

        public JointSide HandSide { get; private set; }

        public JointType JointType { get; private set; }
    }

    public class RotateDirectionRestriction : SimpleBodyRestriction
    {
        public RotateDirectionRestriction(JointType jointType, Z3Point3D startPoint, int degrees, Direction direction)
            : base(body =>
            {
                ArithExpr currentValue;
                var targetValue = 0.0;
                var directionSign = 1.0;
                var currentPoint = body.Joints[jointType];
                CalcCurrentAndTargetValue(currentPoint, startPoint, degrees, direction,
                    out currentValue, out targetValue, out directionSign);
                
                BoolExpr expr = Z3.Context.MkTrue();

                switch (direction)
                {
                    case Direction.Right:   
                    case Direction.Up:      
                    case Direction.Front:   
                        expr = Z3.Context.MkGt(currentValue, Z3Math.Real(targetValue)); 
                        break;
                    case Direction.Left:   
                    case Direction.Down:   
                    case Direction.Back:    
                        expr = Z3.Context.MkLt(currentValue, Z3Math.Real(targetValue)); break;
                }
                return expr;
            },
            body =>
            {
                ArithExpr currentValue;
                var targetValue = 0.0;
                var directionSign = 1.0;
                var currentPoint = body.Joints[jointType];
                CalcCurrentAndTargetValue(currentPoint, startPoint, degrees, direction, 
                    out currentValue, out targetValue, out directionSign);

                var percentage = PercentageCalculator.calc(
                    -1*directionSign, 
                    targetValue, 
                    Z3Math.GetRealValue(currentValue));

                return percentage;
            },
            jointType)
        {
            this.JointType = jointType;
            this.Direction = direction;
            this.Degrees = degrees;
        }

        private static void CalcCurrentAndTargetValue(
            Z3Point3D currentPoint,
            Z3Point3D startPoint,
            int degrees,
            Direction direction,
            out ArithExpr currentValue,
            out double targetValue,
            out double directionSign)
        {
            // once it is rotating towards a direction there is a limit for the rotation
            // in this case the limit is imposed to a single component (X, Y or Z) relative to the direction
            var limitValue = Math.Sin(75 * Math.PI / 180.0);
            var radians = degrees * Math.PI / 180.0; // assigning a double for angle in radians

            // determining if the direction sign is negative
            directionSign = 1.0;
            switch (direction)
            {
                case Direction.Down:
                case Direction.Back:
                case Direction.Left:
                    directionSign = -1.0;
                    break;
            }

            // update limit based on the direction sign
            limitValue *= directionSign;

            // start value stores the component (X, Y or Z) from the startPoint
            // determining the current and start value      
            currentValue = currentPoint.X;
            var startValue = 0.0;
            switch (direction)
            {
                case Direction.Right:
                case Direction.Left:
                    startValue = startPoint.GetXValue();
                    currentValue = currentPoint.X;
                    break;
                case Direction.Up:
                case Direction.Down:
                    startValue = startPoint.GetYValue();
                    currentValue = currentPoint.Y;
                    break;
                case Direction.Front:
                case Direction.Back:
                    startValue = startPoint.GetZValue();
                    currentValue = currentPoint.Z;
                    break;
            }

            double startRadians = Math.Asin(startValue);
            double targetRadians = startRadians + (directionSign * radians);
            targetValue = Math.Sin(targetRadians);

            // this first case tells that the rotation is bigger than the desired and 
            // is moving the vector (targetValue) on the opposite direction
            // this rotation is not desired here because we are rotating towards a direction
            // the rotation is not a pure transform            
            var targetIsLowerThanStart =
                directionSign * targetValue <
                directionSign * startValue;

            // this second case tells that the rotation exceeded the limitValue
            var targetExceedsLimit = Math.Abs(targetValue) > Math.Abs(limitValue);

            // on both cases the targetValue should be the limitValue
            if (targetIsLowerThanStart || targetExceedsLimit)
            {
                targetValue = limitValue;
            }
        }

        public override string ToString()
        {
            return string.Format("rotate your {0} {1} degrees {2}", 
                EnumUtil.GetDescription<JointType>(this.JointType), this.Degrees, 
                EnumUtil.GetDescription<Direction>(this.Direction));
        }

        public JointType JointType { get; set; }

        public int Degrees { get; set; }

        public Direction Direction { get; set; }
    }

    public class PutBodyRestriction : SimpleBodyRestriction
    {
        public PutBodyRestriction(JointType jointType1, JointType jointType2, RelativeDirection direction, bool dont = false)
            : base(body =>
            {
                var distanceThreshold = Z3Math.Real(0.01);
                
                var joint1Position = body.GetJointPosition(jointType1);
                var joint2Position = body.GetJointPosition(jointType2);

                var expr = Z3.Context.MkTrue();

                switch (direction)
                {
                    case RelativeDirection.InFrontOfYour:
                        expr = Z3.Context.MkGt(joint1Position.Z, Z3Math.Add(joint2Position.Z, distanceThreshold));
                        break;

                    case RelativeDirection.BehindYour:
                        expr = Z3.Context.MkLt(joint1Position.Z, Z3Math.Sub(joint2Position.Z, distanceThreshold));
                        break;

                    case RelativeDirection.ToTheRightOfYour:
                        expr = Z3.Context.MkGt(joint1Position.X, Z3Math.Add(joint2Position.X, distanceThreshold));
                        break;

                    case RelativeDirection.ToTheLeftOfYour:
                        expr = Z3.Context.MkLt(joint1Position.X, Z3Math.Sub(joint2Position.X, distanceThreshold));
                        break;

                    case RelativeDirection.OnTopOfYour:
                        expr = Z3.Context.MkGt(joint1Position.Y, Z3Math.Add(joint2Position.Y, distanceThreshold));
                        break;

                    case RelativeDirection.BelowYour:
                        expr = Z3.Context.MkLt(joint1Position.Y, Z3Math.Sub(joint2Position.Y, distanceThreshold));
                        break;
                }

                if (dont) expr = Z3.Context.MkNot(expr);
                return expr;
            },
            body =>
            {
                var distanceThreshold = 0.01;

                var joint1Position = body.GetJointPosition(jointType1);
                var joint2Position = body.GetJointPosition(jointType2);
                
                var point1 = new Point3D(
                    joint1Position.GetXValue(),
                    joint1Position.GetYValue(),
                    joint1Position.GetZValue());

                var point2 = new Point3D(
                    joint2Position.GetXValue(),
                    joint2Position.GetYValue(),
                    joint2Position.GetZValue());

                var targetValue = 1.0;
                var currentValue = 1.0;
                var lowerBound = 0.0;

                // inverting direction if expression is negated
                if (dont)
                {
                    switch (direction)
                    {
                        case RelativeDirection.ToTheRightOfYour: direction = RelativeDirection.ToTheLeftOfYour; break;
                        case RelativeDirection.ToTheLeftOfYour:  direction = RelativeDirection.ToTheRightOfYour; break;
                        case RelativeDirection.OnTopOfYour:      direction = RelativeDirection.BelowYour; break;
                        case RelativeDirection.BelowYour:        direction = RelativeDirection.OnTopOfYour; break;
                        case RelativeDirection.InFrontOfYour:    direction = RelativeDirection.BehindYour; break;
                        case RelativeDirection.BehindYour:       direction = RelativeDirection.InFrontOfYour; break;
                    }
                }

                switch (direction)
                {
                    case RelativeDirection.ToTheRightOfYour:
                        currentValue = point1.X;
                        targetValue = point2.X + distanceThreshold;
                        lowerBound = -1.0;
                        break;

                    case RelativeDirection.ToTheLeftOfYour:
                        currentValue = point1.X;
                        targetValue = point2.X - distanceThreshold;
                        lowerBound = 1.0;
                        break;

                    case RelativeDirection.OnTopOfYour:
                        currentValue = point1.Y;
                        targetValue = point2.Y + distanceThreshold;
                        lowerBound = -1.0;
                        break;

                    case RelativeDirection.BelowYour:
                        currentValue = point1.Y;
                        targetValue = point2.Y - distanceThreshold;
                        lowerBound = 1.0;
                        break;

                    case RelativeDirection.InFrontOfYour:
                        currentValue = point1.Z;
                        targetValue = point2.Z + distanceThreshold;
                        lowerBound = -1.0;
                        break;

                    case RelativeDirection.BehindYour:
                        currentValue = point1.Z;
                        targetValue = point2.Z - distanceThreshold;
                        lowerBound = 1.0;
                        break;
                }

                var percentage = PercentageCalculator.calc(lowerBound, targetValue, currentValue);                
                return percentage;
            },
            jointType1,
            jointType2)
        {
            this.JointType1 = jointType1;
            this.JointType2 = jointType2;
            
            this.Direction = direction;

            if (dont)
            {
                this.isNegated = true; 
            }
            else
            {
                this.isNegated = false; 
            }
        }

        public override string ToString()
        {
            return string.Format("put your {0} {1} {2}", 
                EnumUtil.GetDescription<JointType>(this.JointType1), 
                EnumUtil.GetDescription<RelativeDirection>(this.Direction),
                EnumUtil.GetDescription<JointType>(this.JointType2));
        }

        public JointType JointType1 { get; set; }

        public JointType JointType2 { get; set; }

        public RelativeDirection Direction { get; set; }
    }

    public class AlignBodyRestriction : SimpleBodyRestriction
    {
        public AlignBodyRestriction(JointType jointType1, JointType jointType2, int degreesThreshold = 20, bool dont = false) :
            base((body =>
             {
                 var joint1 = body.Joints[jointType1];
                 var joint2 = body.Joints[jointType2];

                 BoolExpr expr = joint1.IsDegreesBetweenLessThan(joint2, degreesThreshold);
                 if (dont) expr = Z3.Context.MkNot(expr);
                 return expr;
             }),
            (body =>
             {
                 var joint1 = body.Joints[jointType1];
                 var joint2 = body.Joints[jointType2];

                 var vec1 = new Point3D(joint1.GetXValue(), joint1.GetYValue(), joint1.GetZValue());
                 var vec2 = new Point3D(joint2.GetXValue(), joint2.GetYValue(), joint2.GetZValue());

                 var degrees = Math.Abs(vec1.RadiansTo(vec2) * 180/Math.PI);

                 var percentage = degreesThreshold / degrees;
                 if (dont) percentage = degrees / degreesThreshold;

                 percentage = Math.Max(0.0, Math.Min(1.0, percentage));

                 return percentage;
             }),
            jointType1, jointType2)
        {
            this.JointType1 = jointType1;
            this.JointType2 = jointType2;
            if (dont)
            {
                this.isNegated = true; 
            }
            else
            {
                this.isNegated = false; 
            }
        }

        public override string ToString()
        {
            return string.Format("align your {0} and your {1}", 
                EnumUtil.GetDescription<JointType>(this.JointType1), 
                EnumUtil.GetDescription<JointType>(this.JointType2));
        }

        public JointType JointType1 { get; set; }

        public JointType JointType2 { get; set; }
    }

    public class PercentageCalculator
    {
        public static double calc(double lowerBound, double targetValue, double currentValue)
        {
            var percentage = (currentValue - lowerBound)/(targetValue - lowerBound);
            percentage = Math.Min(1.0,(Math.Max(0.0, percentage)));
            return percentage;
        }
    }

    // TODO Create restriction to filter invalid bodies (e.g.: with (0, 0, 0) points)
}
