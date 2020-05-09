using System.Linq;

public class MethodCall
{
	public string Method { get; }
	public MethodArgument[] Args { get; }

	public MethodCall(string method, MethodArgument[] args)
	{
		Method = method;
		Args = args;
	}

	public override string ToString()
	{
		return $"{Method}({string.Join<MethodArgument>(", ", Args)}";
	}
}