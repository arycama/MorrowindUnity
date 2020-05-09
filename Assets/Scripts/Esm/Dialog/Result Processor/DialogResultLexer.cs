using System.Collections.Generic;
using System.Linq;
using System.Text;

public class DialogResultLexer
{
	private string source;
	private int index, line, column;
	private StringBuilder stringBuilder = new StringBuilder();

	private char Current => source[index];
	private char Last => Peek(-1);
	private char Next => Peek(1);

	private bool IsEof => index == source.Length;
	private bool IsDigit => char.IsDigit(Current);
	private bool IsIdentifier => IsLetterOrDigit || Current == '_';
	private bool IsLetter => char.IsLetter(Current);
	private bool IsLetterOrDigit => char.IsLetterOrDigit(Current);
	private bool IsPunctuation => "<>{}()[]!%^&*+-=/.,?;:|".Contains(Current);
	private bool IsNewLine => (Current == '\r' && Peek(1) == '\n');
	private bool IsWhiteSpace => char.IsWhiteSpace(Current) && !IsNewLine;

	public DialogResultLexer(string source)
	{
		this.source = source;
	}

	public IEnumerable<Token> ProcessResult()
	{
		index = 0;
		line = 0;
		column = 0;

		while (index < source.Length)
		{
			// Check if new line
			if(IsNewLine)
			{
				yield return ScanNewLine();
			}
			else if (IsWhiteSpace)
			{
				yield return ScanWhiteSpace();
			}
			else if (IsDigit || Current == '-' && char.IsDigit(Peek(1)))
			{
				yield return ScanInteger();
			}
			else if (IsLetter || Current == '_')
			{
				yield return ScanIdentifier();
			}
			else if (Current == '"')
			{
				yield return ScanStringLiteral();
			}
			//else if (Current == '.' && char.IsDigit(Next))
			//{
			//	return ScanFloat();
			//}
			else if (Current == '=' && Next == '>')
			{
				Consume();
				Consume();
				yield return CreateToken(TokenKind.Arrow);
			}
			else
			{
				yield return ScanStringWithoutQuotes();
			}
		}
	}

	private void Consume()
	{
		stringBuilder.Append(Current);
		index++;
		column++;
	}

	private char Peek(int ahead) => source[index + ahead];

	private Token CreateToken(TokenKind kind)
	{
		var contents = stringBuilder.ToString();
		stringBuilder.Clear();
		return new Token(contents, kind);
	}

	private Token ScanNewLine()
	{
		// Consume twice to account for the carriage-return and newline characters
		Consume();
		Consume();
		line++;
		column = 0;

		return CreateToken(TokenKind.NewLine);
	}

	private Token ScanWhiteSpace()
	{
		while (IsWhiteSpace)
		{
			Consume();
		}

		return CreateToken(TokenKind.WhiteSpace);
	}

	private Token ScanInteger()
	{
		// Check for negative sign
		if (Current == '-')
		{
			Consume();
		}

		while (!IsEof && IsDigit)
		{
			Consume();
		}

		return CreateToken(TokenKind.Integer);
	}

	private Token ScanIdentifier()
	{
		while (IsIdentifier)
		{
			Consume();
		}

		// First check that this isn't a rem/comment
		if (stringBuilder.ToString() == ";")
		{
			while (!IsNewLine)
			{
				Consume();
			}

			return ScanNewLine();
		}

		return CreateToken(TokenKind.Identifier);
	}

	private Token ScanStringLiteral()
	{
		// Skip the first quote
		index++;

		while (Current != '"')
		{
			Consume();
		}

		// Skip the final quote
		index++;

		return CreateToken(TokenKind.String);
	}

	private Token ScanStringWithoutQuotes()
	{
		while (!IsNewLine)
		{
			Consume();
		}

		return CreateToken(TokenKind.String);
	}
}