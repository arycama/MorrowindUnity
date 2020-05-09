using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class RepairService : CharacterService
{
	public override string ServiceName => "Repair";

	protected override string ServiceTitle => "Repair Service Menu";

	protected override string Description => "Select item to repair";

	public static RepairService Create(GameObject gameObject) => gameObject.AddComponent<RepairService>();

	protected override IEnumerable<ServiceOption> GetOptions(Character player, Character npc)
	{
		return new List<ServiceOption>
		{
			new ServiceOption("Bonemold Right Bracer - 140gp", null, 0),
			new ServiceOption("Expeliarmus - 93gp", null, 0),
			new ServiceOption("Lumos - 46gp", null, 0),
		};
	}
}