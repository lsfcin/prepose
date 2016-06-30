using Antlr4.Runtime;
using PreposeGestures.Parser;
using System;
using System.Collections.Generic;

namespace PreposeGestures
{
    public class SyntaxErrorException : Exception
    {
        public SyntaxErrorException(string message)
        : base(message)
        {
        }
    }

	public class ParserErrorListener <Symbol> : IAntlrErrorListener<Symbol>
    {
        public virtual void SyntaxError(IRecognizer recognizer, Symbol offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
        {
            throw new SyntaxErrorException("[line " + line + ", column " + (charPositionInLine + 1) + "] " + msg);
        }
    }

    /// <summary>
    /// Representation of the parsed code 
    /// </summary>
	public class App
	{
		public IList<Gesture> Gestures = new List<Gesture>();
        public App(string name)
        {
			this.Name = name;
		}

        public App(string name, IList<Gesture> gestures, int precision = 15)
		{
			this.Name = name;
            this.Gestures = gestures;
		}

		public string Name { get; private set; }

		public override string ToString()
		{
			return string.Format("APP {0} = \n\t{1}", Name, string.Join("\n\n\t", this.Gestures));
		}

		public static App ReadApp(string filename)
		{
			var input = new Antlr4.Runtime.AntlrFileStream(filename);
            var app = GestureAppFromAntlrInput(input);

			return app;
		}

		public static App ReadAppText(string inputString)
		{
			var input = new Antlr4.Runtime.AntlrInputStream(inputString);
            var app = GestureAppFromAntlrInput(input);

			return app;
		}

        private static App GestureAppFromAntlrInput(ICharStream input)
        {
            var lexer = new PreposeGesturesLexer(input);
            var tokens = new CommonTokenStream(lexer);
            var parser = new PreposeGesturesParser(tokens);
            parser.AddErrorListener((IAntlrErrorListener<IToken>)new ParserErrorListener<IToken>());

            var tree = parser.app(); // parse
            var visitor = new AppConverter();
            var app = (App)visitor.Visit(tree);
           
            return app;
        }
    }
}
