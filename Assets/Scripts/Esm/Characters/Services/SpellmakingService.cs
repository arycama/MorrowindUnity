using System.Collections.Generic;
using UnityEngine;

public class SpellmakingService : CharacterService
{
	public override string ServiceName => "Spellmaking";

	public static RepairService Create(GameObject gameObject) => gameObject.AddComponent<RepairService>();

	protected override IEnumerable<ServiceOption> GetOptions(Character player, Character npc)
	{
		throw new System.NotImplementedException();
	}
}
