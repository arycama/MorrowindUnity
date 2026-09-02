using Unity.Collections;
using UnityEngine.Rendering;

public readonly struct NativePassDescriptor
{
	public readonly NativeArray<RenderTargetHandle> attachments;
	public readonly NativeArray<SubPassDescriptor> subpasses;
	public readonly int depthIndex;
	public readonly int passEndIndex;
	public readonly string debugName;
	public readonly int depthSlice;
	public readonly int volumeDepth;

	public NativePassDescriptor(NativeArray<RenderTargetHandle> attachments, NativeArray<SubPassDescriptor> subpasses, int depthIndex, int passEndIndex, int depthSlice, int volumeDepth, string debugName)
	{
		this.attachments = attachments;
		this.subpasses = subpasses;
		this.depthIndex = depthIndex;
		this.passEndIndex = passEndIndex;
		this.depthSlice = depthSlice;
		this.volumeDepth = volumeDepth;
		this.debugName = debugName;
	}
}
