using System;
using UnityEngine.Rendering;

public interface IRenderPass
{
	string Name { get; set; }
	Range ResourceRange { get; set; }
	ViewHandle ViewHandle { get; set; }
	int NativePassIndex { get; set; }
	bool IsNewSubPass { get; set; }

	void Execute(CommandBuffer command);
}
