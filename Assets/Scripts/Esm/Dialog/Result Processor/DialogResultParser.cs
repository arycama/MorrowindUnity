using System.Collections.Generic;

public class DialogResultParser
{
	private bool isEOF;
	private IEnumerator<Token> enumerator;
	private Token current;

	public IEnumerable<MethodCall> ParseFile(IEnumerable<Token> tokens)
	{
		enumerator = tokens.GetEnumerator();
		enumerator.MoveNext();
		current = enumerator.Current;

		while (!isEOF)
		{
			switch (current.Kind)
			{
				case TokenKind.Identifier:
					yield return ProcessMethodCall();
					break;
			}

			Advance();
		}
	}

	private void Advance()
	{
		if (enumerator.MoveNext())
		{
			current = enumerator.Current;
		}
		else
		{
			isEOF = true;
		}
	}

	private void Take(TokenKind kind)
	{
		Advance();
		if (current.Kind != kind)
		{
			throw new System.Exception("Unexpected Token" + current.Kind.ToString());
		}
	}

	private MethodCall ProcessMethodCall()
	{
		// Take the class and method info
		var methodName = current.Contents;
		Take(TokenKind.WhiteSpace);

		// Get parameters
		var arguments = new List<MethodArgument>();
		do
		{
			Advance();

			if (isEOF)
			{
				break;
			}

			switch (current.Kind)
			{
				case TokenKind.Identifier:
					var arg = "";
					do
					{
						arg += current.Contents;
						Advance();
					}
					while (current.Kind != TokenKind.WhiteSpace && current.Kind != TokenKind.NewLine);
					arguments.Add(new MethodArgument(ArgumentType.String, arg));
					break;
				case TokenKind.Integer:
					arguments.Add(new MethodArgument(ArgumentType.Int, current.IntValue));
					break;
				case TokenKind.Float:
					arguments.Add(new MethodArgument(ArgumentType.Float, current.FloatValue));
					break;
				case TokenKind.String:
					arguments.Add(new MethodArgument(ArgumentType.String, current.Contents));
					break;
			}
		}
		while (current.Kind != TokenKind.NewLine && !isEOF);

		return new MethodCall(methodName, arguments.ToArray());
	}
}