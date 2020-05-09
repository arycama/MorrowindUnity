using System;
using UnityEngine;

namespace Esm
{
	// Stores a faction's name, and a reaction modifider
	[Serializable]
	public class FactionReaction
	{
		[SerializeField]
		private Faction faction;

		[SerializeField]
		private int reaction;

		public FactionReaction(Faction faction, int reaction)
		{
			this.faction = faction;
			this.reaction = reaction;
		}

		public int Reaction => reaction;
		public Faction Faction => faction;
	}
}