using System;

namespace Esm
{
	[Serializable]
		public class CreatureData
		{
			private readonly int level, soul, combat, magic, stealth, attackMin1, attackMax1, attackMin2, attackMax2, attackMin3, attackMax3, gold;
			private readonly CreatureType type;
			private readonly BaseAttributeData baseAttributeData;
			private readonly DerivedAttributeData derivedAttributeData;
			private readonly AttackData[] attackData = new AttackData[3];

			public CreatureData(System.IO.BinaryReader reader)
			{
				type = (CreatureType)reader.ReadInt32();
				level = reader.ReadInt32();
				baseAttributeData = new BaseAttributeData(reader, true);
				derivedAttributeData = new DerivedAttributeData(reader, true);
				soul = reader.ReadInt32();
				combat = reader.ReadInt32();
				magic = reader.ReadInt32();
				stealth = reader.ReadInt32();

				for (var i = 0; i < attackData.Length; i++)
				{
					attackData[i] = new AttackData(reader);
				}

				gold = reader.ReadInt32();
			}

			private struct AttackData
			{
				private readonly int minimumDamage, maximumDamage;

				public AttackData(System.IO.BinaryReader reader)
				{
					minimumDamage = reader.ReadInt32();
					maximumDamage = reader.ReadInt32();
				}
			}
		}
	
}