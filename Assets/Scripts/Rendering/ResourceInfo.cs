using System.Diagnostics;

[DebuggerDisplay("id({propertyId}), resource({resourceIndex}), writes({firstWriteIndex}:{lastWriteIndex}), lastRead({lastReadIndex})")]
public struct ResourceInfo
{
	public int descriptorIndex;
	public bool isExported;
	public int resourceIndex;
	public int firstWriteIndex;
	public int lastWriteIndex;
	public int lastReadIndex;
	public int propertyId;
	public ResourceHandleType type;

	public ResourceInfo(int descriptorIndex, int propertyId, ResourceHandleType type)
	{
		this.descriptorIndex = descriptorIndex;
		this.propertyId = propertyId;
		this.type = type;
		isExported = false;
		resourceIndex = -1;
		firstWriteIndex = -1;
		lastWriteIndex = -1;
		lastReadIndex = -1;
	}
}
