using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PreposeGestures
{
    // delayed statements can be transforms or restrictions
    // delayed statements can't be fully defined on the parsing / visiting
    // sample reasons are
    // there must be an input body
    public class CompositeDelayedStatement
    {
        public CompositeDelayedStatement()
        {
            Statements = new List<RotateDelayedStatement>();
        }

        public CompositeDelayedStatement(RotateDelayedStatement statement) : this()
        {
            Statements.Add(statement);
        }

        public CompositeDelayedStatement Compose(CompositeDelayedStatement that)
        {
            var result = new CompositeDelayedStatement();
            result.Statements.AddRange(this.Statements);
            result.Statements.AddRange(that.Statements);
            return result;
        }

        internal void Update(Z3Body body)
        {
            this.Restriction = new CompositeBodyRestriction();
            this.Transform = new CompositeBodyTransform();            

            // treating rotate direction
            // for each definition
                // if a joint appears once 
                    // restrict
                // else
                    // transform

            // the rotate direction can be a transform or a restriction            
            foreach (var definition in this.Statements)
            {
                var jointCount = 0;
                foreach (var definitionTemp in this.Statements)                    
                {
                    if (definition.JointType == definitionTemp.JointType)
                        jointCount++;
                }

                // if for a single joint there is only one rotate direction
                // then the restriction should be applied to give more 
                // freedom for the user while performing the gesture
                if(jointCount == 1)
                {                      
                    this.Restriction.And(new RotateDirectionRestriction(
                            definition.JointType, body.Joints[definition.JointType],
                            definition.Degrees, definition.Direction));                    
                }
               
                // else if for that joint there is more than one rotate direction
                // then we create a transform to guarantee that all directions
                // are well represented
                else if(jointCount > 1)
                {
                    this.Transform = this.Transform.Compose(
                        new CompositeBodyTransform(definition.JointType,
                            new RotateJointTransform(definition.Degrees, definition.Direction)));
                }       
            }
        }

		public override string ToString()
        {
            var result = "";
            var count = 0;
            foreach(var statement in Statements)
            {
                if (count > 0)
                    result += "\n";
                result += statement.ToString();
                count++;
            }
            return result;
        }

        public CompositeBodyTransform Transform { get; set; }
        public CompositeBodyRestriction Restriction { get; set; }
        public List<RotateDelayedStatement> Statements { get; set; }
    }

    public class RotateDelayedStatement
    {
        public RotateDelayedStatement(JointType jointType, Direction direction, int degrees)
        {
            this.JointType = jointType;
            this.Direction = direction;
            this.Degrees = degrees;
        }

        public JointType JointType { get; set; }
        public Direction Direction { get; set; }
        public int Degrees { get; set; }

		public override string ToString()
        {
            var result = "rotate your " + JointType.ToString() + " " + Degrees + " degrees " + Direction.ToString();
            return result.ToLower();
        }
    }
}
