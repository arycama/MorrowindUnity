using System.Diagnostics;

[DebuggerDisplay("Desc: {descriptor}, Resource: {resourceIndex}, FirstWrite: {firstWriteIndex}, LastRead: {lastReadIndex}")]
public struct RenderTargetInfo
{
	public RenderTargetDescriptor descriptor;
	public int resourceIndex;
	public int firstWriteIndex;
	public int lastReadIndex;
	public int id;
	public bool dontResolve;

	public RenderTargetInfo(RenderTargetDescriptor descriptor, int resourceIndex, int firstWriteIndex, int lastReadIndex, int id, bool dontResolve)
	{
		this.descriptor = descriptor;
		this.resourceIndex = resourceIndex;
		this.firstWriteIndex = firstWriteIndex;
		this.lastReadIndex = lastReadIndex;
		this.id = id;
		this.dontResolve = dontResolve;
	}
}