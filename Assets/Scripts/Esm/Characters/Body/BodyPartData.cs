using System;
using UnityEngine;

namespace Esm
{
	[Serializable]
	public class BodyPartData
	{
		[SerializeField, EnumFlags]
		private BodyPartFlags flags;

		[SerializeField]
		private BodyPartType partType;

		[SerializeField]
		private BodyPartPiece part;

		[SerializeField]
		private SkinType skinType;

		public BodyPartData(System.IO.BinaryReader reader)
		{
			part = (BodyPartPiece)reader.ReadByte();
			skinType = (SkinType)reader.ReadByte();
			flags = (BodyPartFlags)reader.ReadByte();
			partType = (BodyPartType)reader.ReadByte();
		}

		public void SetBodyPart(Race race, BodyPartRecord bodyPartRecord)
		{
			// Skins can be clothing, armor or skin. We only want to save skin types to the race list
			if (partType == BodyPartType.Skin)
			{
				RaceBody body;
				if (!BodyPartRecord.RaceBodyParts.TryGetValue(race, out body))
				{
					body = new RaceBody();
					BodyPartRecord.RaceBodyParts.Add(race, body);
				}

				body.AddPart(part, bodyPartRecord, flags.HasFlag(BodyPartFlags.Female));
			}
		}
	}
}