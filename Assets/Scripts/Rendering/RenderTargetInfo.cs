using System.Diagnostics;

[DebuggerDisplay("Desc: {descriptor}, Resource: {resourceIndex}, FirstWrite: {firstWriteIndex}, LastRead: {lastReadIndex}")]
public struct RenderTargetInfo
{
	public RenderTargetDescriptor descriptor;
	public bool isExported;
	public int resourceIndex;
	public int firstWriteIndex;
	public int lastWriteIndex;
	public int lastReadIndex;
	public int propertyId;

	public RenderTargetInfo(RenderTargetDescriptor descriptor, int propertyId)
	{
		this.descriptor = descriptor;
		this.propertyId = propertyId;
		isExported = false;
		resourceIndex = -1;
		firstWriteIndex = -1;
		lastWriteIndex = -1;
		lastReadIndex = -1;
	}
}