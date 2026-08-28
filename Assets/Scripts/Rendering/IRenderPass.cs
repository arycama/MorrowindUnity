using System;
using System.Collections.Generic;
using UnityEngine.Rendering;

public interface IRenderPass
{
	string Name { get; set; }
	Range ResourceRange { get; set; }
	ViewHandle ViewHandle { get; set; }
	int NativePassIndex { get; set; }
	bool IsNewSubPass { get; set; }

	// TODO: Replace
	List<string> Keywords { get; set; }

	void Execute(CommandBuffer command);
}
