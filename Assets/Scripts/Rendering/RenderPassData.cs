using System;

public struct RenderPassData
{
	public Range resourceRange;
	public string Name;
	public ViewHandle ViewHandle;
	public int NativePassIndex;
	public bool IsNewSubPass;
}