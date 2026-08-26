using System;
using UnityEngine.Rendering;

public interface IRenderPass
{
	string Name { get; }
	ViewHandle ViewHandle { get; }
	Range ResourceRange { get; }
	int NativePassIndex { get; }
	bool IsNewSubPass { get; }

	void Execute(CommandBuffer command);
}
