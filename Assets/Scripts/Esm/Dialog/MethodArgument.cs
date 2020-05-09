using UnityEngine;

public class MethodArgument
{
	public MethodArgument(ArgumentType type, object value)
	{
		Type = type;
		Value = value;
	}

	public ArgumentType Type { get; }
	public object Value { get; }

	public float FloatValue => (float)Value;
	public int IntValue => (int)Value;
	public string StringValue => (string)Value;

	public override string ToString() => $"{Value} ({Type})";
}