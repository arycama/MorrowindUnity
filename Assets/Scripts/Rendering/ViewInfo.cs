using System.Diagnostics;
using Unmath;

[DebuggerDisplay("{size} {samples}aa")]
public readonly struct ViewInfo
{
	public readonly Int2 size;
	public readonly int samples;

	public ViewInfo(Int2 size, int samples)
	{
		this.size = size;
		this.samples = samples;
	}
}