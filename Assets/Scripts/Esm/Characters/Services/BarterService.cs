using System.Collections.Generic;
using UnityEngine;

public class BarterService : CharacterService
{
	public override string ServiceName => "Barter";

	public static BarterService Create(GameObject gameObject) => gameObject.AddComponent<BarterService>();

	protected override IEnumerable<ServiceOption> GetOptions(Character player, Character npc)
	{
		throw new System.NotImplementedException();
	}
}
