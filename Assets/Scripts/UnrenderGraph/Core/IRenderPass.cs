using System;
using System.Collections.Generic;
using UnityEngine.Rendering;

public interface IRenderPass
{
	string Name { get; set; }
	Range ResourceRange { get; set; }
	Range UavResourceRange { get; set; }
	ViewHandle ViewHandle { get; set; }
	int NativePassIndex { get; set; }
	bool IsNewSubPass { get; set; }

	// TODO: Replace
	List<GlobalKeyword> Keywords { get; set; }

	void Execute(CommandBuffer command);
}
