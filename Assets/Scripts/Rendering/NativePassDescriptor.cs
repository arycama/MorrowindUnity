using Unity.Collections;
using UnityEngine.Rendering;

public readonly struct NativePassDescriptor
{
	public readonly NativeArray<TextureHandle> attachments;
	public readonly NativeArray<SubPassDescriptor> subpasses;
	public readonly int depthIndex;
	public readonly int passEndIndex;
	public readonly string debugName;

	public NativePassDescriptor(NativeArray<TextureHandle> attachments, NativeArray<SubPassDescriptor> subpasses, int depthIndex, int passEndIndex, string debugName)
	{
		this.attachments = attachments;
		this.subpasses = subpasses;
		this.depthIndex = depthIndex;
		this.passEndIndex = passEndIndex;
		this.debugName = debugName;
	}
}
