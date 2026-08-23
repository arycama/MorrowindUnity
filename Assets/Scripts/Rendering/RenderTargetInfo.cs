using System.Diagnostics;

[DebuggerDisplay("Desc: {descriptor}, Resource: {resourceIndex}, FirstWrite: {firstWriteIndex}, LastRead: {lastReadIndex}")]
public struct RenderTargetInfo
{
	public RenderTargetDescriptor descriptor;
	public int resourceIndex;
	public int firstWriteIndex;
	public int lastReadIndex;
	public bool dontResolve;
	public bool isImported;

	public RenderTargetInfo(RenderTargetDescriptor descriptor, int resourceIndex, int firstWriteIndex, int lastReadIndex, bool dontResolve, bool isImported)
	{
		this.descriptor = descriptor;
		this.resourceIndex = resourceIndex;
		this.firstWriteIndex = firstWriteIndex;
		this.lastReadIndex = lastReadIndex;
		this.dontResolve = dontResolve;
		this.isImported = isImported;
	}
}