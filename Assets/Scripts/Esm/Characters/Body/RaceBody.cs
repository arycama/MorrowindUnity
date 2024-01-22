using System;
using System.Collections.Generic;

namespace Esm
{
	/// <summary>
	/// Contains all the body parts for a particular race. 
	/// </summary>
	[Serializable]
	public class RaceBody
	{
		private readonly Dictionary<BodyPartPiece, BodyPartRecord> maleParts = new Dictionary<BodyPartPiece, BodyPartRecord>(13),
			femaleParts = new Dictionary<BodyPartPiece, BodyPartRecord>(13);

		private readonly List<BodyPartRecord>
			maleHeads = new List<BodyPartRecord>(),
			femaleHeads = new List<BodyPartRecord>(),
			maleHair = new List<BodyPartRecord>(),
			femaleHair = new List<BodyPartRecord>();

		private BodyPartRecord maleHands1st, femaleHands1st;

		public void AddPart(BodyPartPiece bodyPartType, BodyPartRecord bodyPart, bool isFemale)
		{
			if (isFemale)
			{
				switch (bodyPartType)
				{
					case BodyPartPiece.Head:
						if (isFemale) femaleHeads.Add(bodyPart);
						else maleHeads.Add(bodyPart);
						return;
					case BodyPartPiece.Hair:
						femaleHair.Add(bodyPart);
						return;
					case BodyPartPiece.Hand:
						if (bodyPart.name.EndsWith(".1st"))
						{
							femaleHands1st = bodyPart;
						}
						else
						{
							femaleParts[BodyPartPiece.Hand] = bodyPart;
						}
						return;
					default:
						femaleParts.Add(bodyPartType, bodyPart);
						return;
				}
			}

			switch (bodyPartType)
			{
				case BodyPartPiece.Head:
					maleHeads.Add(bodyPart);
					return;
				case BodyPartPiece.Hair:
					maleHair.Add(bodyPart);
					return;
				case BodyPartPiece.Hand:
					if (bodyPart.name.EndsWith(".1st"))
					{
						maleHands1st = bodyPart;
					}
					else
					{
						maleParts[BodyPartPiece.Hand] = bodyPart;
					}
					return;
				default:
					maleParts.Add(bodyPartType, bodyPart);
					return;
			}
		}

		public BodyPartRecord GetPart(BodyPartPiece bodyPartType, bool isFemale)
		{
			if (isFemale)
			{
				BodyPartRecord bodyPart;
				if (femaleParts.TryGetValue(bodyPartType, out bodyPart))
				{
					return femaleParts[bodyPartType];
				}

				return maleParts[bodyPartType];
			}

			return maleParts[bodyPartType];
		}
	}
}