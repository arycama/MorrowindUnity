using System.Diagnostics;

[DebuggerDisplay("{index}")]
public readonly struct ViewHandle
{
	public readonly int index;

	public ViewHandle(int index)
	{
		this.index = index;
	}
}
