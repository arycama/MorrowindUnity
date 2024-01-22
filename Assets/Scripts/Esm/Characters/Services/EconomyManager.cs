using System;
using System.Collections.Generic;
using Esm;

public static class EconomyManager
{
	public static MiscItemRecord Gold => Record.GetRecord<MiscItemRecord>("Gold_001");

	private static readonly HashSet<string> GoldIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
	{
		"Gold_001",
		"Gold_005",
		"Gold_010",
		"Gold_025",
		"Gold_100"
	};

	public static bool IsGold(MiscItemRecord record)
	{
		return GoldIds.Contains(record.name);
	}
}