using System.Collections.Generic;
using UnityEngine;

public class EnchantingService : CharacterService
{
	public override string ServiceName => "Enchanting";

	public static EnchantingService Create(GameObject gameObject) => gameObject.AddComponent<EnchantingService>();

	protected override IEnumerable<ServiceOption> GetOptions(Character player, Character npc)
	{
		throw new System.NotImplementedException();
	}
}