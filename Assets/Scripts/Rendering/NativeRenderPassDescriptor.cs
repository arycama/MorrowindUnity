using Unity.Collections;
using UnityEngine.Rendering;

public readonly struct NativeRenderPassDescriptor
{
	public readonly NativeArray<TextureHandle> attachments;
	public readonly NativeArray<SubPassDescriptor> subpasses;
	public readonly int depthIndex;
	public readonly string debugName;

	public NativeRenderPassDescriptor(NativeArray<TextureHandle> attachments, NativeArray<SubPassDescriptor> subpasses, int depthIndex, string debugName)
	{
		this.attachments = attachments;
		this.subpasses = subpasses;
		this.depthIndex = depthIndex;
		this.debugName = debugName;
	}
}
