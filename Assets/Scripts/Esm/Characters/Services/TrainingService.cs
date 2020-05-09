using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

public class TrainingService : CharacterService
{
	public override string ServiceName => "Training";

	protected override bool CloseOnPurchase => true;
	protected override bool CloseUIOnPurchase => true;

	protected override string Description => "Select skill to train";

	public static TrainingService Create(GameObject gameObject) => gameObject.AddComponent<TrainingService>();

	protected override IEnumerable<ServiceOption> GetOptions(Character player, Character npc)
	{
		// Find the 3 highest skills and display them for training 
		//var highestSkills = new List<Tuple<byte, int>>();
		//for (var i = 0; i < npc.NpcRecord.NpcSubData.Skills.Count; i++)
		//{
		//	var currentSkill = npc.NpcRecord.NpcSubData.Skills[i];

		//	if (currentSkill > highestSkills[2].Item2)
		//	{

		//	}
		//}

		return new List<ServiceOption>
		{
			new ServiceOption("Medium Armor - 140gp", null, 0),
			new ServiceOption("Long Blade - 93gp", null, 0),
			new ServiceOption("Block - 46gp", null, 0),
		};
	}
}