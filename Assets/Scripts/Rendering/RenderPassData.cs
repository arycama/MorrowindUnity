using System;
using System.Diagnostics;

[DebuggerDisplay("{name}, resources: {resourceRange}, viewHandle: {viewHandle}, nativePass: {nativePassIndex}, isNewSubPass: {isNewSubPass}")]
public struct RenderPassData
{
	public string name;
	public Range resourceRange;
	public ViewHandle viewHandle;
	public int nativePassIndex;
	public bool isNewSubPass;
}