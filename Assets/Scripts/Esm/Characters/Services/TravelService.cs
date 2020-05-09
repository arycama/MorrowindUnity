using System;
using System.Collections.Generic;
using UnityEngine;

public class TravelService : CharacterService
{
	public override string ServiceName => "Travel";

	protected override bool CloseOnPurchase => true;
	protected override bool CloseUIOnPurchase => true;
	protected override string Description => "Select Destination";

	public static TravelService Create(GameObject gameObject)
	{
		var component = gameObject.AddComponent<TravelService>();
		return component;
	}

	protected override IEnumerable<ServiceOption> GetOptions(Character player, Character npc)
	{
		var destinations = npc.NpcRecord.GetTravelDestinations();
		var isGuildGuide = npc.NpcRecord.Class.Name == "Guild Guide";

		foreach (var destination in destinations)
		{
			var text = destination.Cell ?? CellManager.GetCellName(destination.DoorExitData.Position);

			float cost;
			if (isGuildGuide)
			{
				cost = GameSetting.Get("fMagesGuildTravel").FloatValue;
			}
			else
			{
				var distance = Vector3.Distance(npc.transform.position, destination.DoorExitData.Position);
				cost = distance / GameSetting.Get("fTravelMult").FloatValue;
			}

			Func<bool> callback = () => { OnClick(destination, player); return true; };
			yield return new ServiceOption(text, callback, cost);
		}
	}

	private void OnClick(TravelDestination destination, Character player)
	{
		if (destination.Cell != null)
			CellManager.LoadCell(destination.Cell);

		destination.DoorExitData.OpenDoor(player.transform);

		//time = int(dist / fTravelTimeMult)
	}
}