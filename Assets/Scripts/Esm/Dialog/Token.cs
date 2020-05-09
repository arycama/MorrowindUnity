public class Token
{
	public Token(string contents, TokenKind kind)
	{
		Kind = kind;
		Contents = contents;
	}

	public TokenKind Kind { get; }
	public string Contents { get; }

	public float FloatValue => float.Parse(Contents);
	public int IntValue => int.Parse(Contents);

	public override string ToString() => $"{Contents} ({Kind})";
}