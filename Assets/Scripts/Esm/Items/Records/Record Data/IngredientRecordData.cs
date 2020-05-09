using System;
using System.Collections.Generic;
using UnityEngine;

namespace Esm
{
	[Serializable]
	public class IngredientRecordData : ItemRecordData
	{
		[SerializeField]
		private MagicEffectRecord[] magicEffects;

		[SerializeField]
		private CharacterAttribute[] characterAttributes;

		[SerializeField]
		private CharacterSkill[] characterSkills;

		public IngredientRecordData(System.IO.BinaryReader reader)
		{
			weight = reader.ReadSingle();
			value = reader.ReadInt32();

			var magicEffects = new List<MagicEffectRecord>();
			for (var i = 0; i < 4; i++)
			{
				var magicEffectType = (MagicEffectType)reader.ReadInt32();

				if(magicEffectType == MagicEffectType.None)
				{
					continue;
				}

				var magicEffectRecord = MagicEffectRecord.Get(magicEffectType);
				magicEffects.Add(magicEffectRecord);
			}

			this.magicEffects = magicEffects.ToArray();

			characterSkills = new CharacterSkill[4];
			for (var i = 0; i < characterSkills.Length; i++)
			{
				characterSkills[i] = (CharacterSkill)reader.ReadInt32();
			}

			characterAttributes = new CharacterAttribute[4];
			for (var i = 0; i < characterAttributes.Length; i++)
			{
				characterAttributes[i] = (CharacterAttribute)reader.ReadInt32();
			}
		}

		public override void DisplayInfo(InfoPanel infoPanel)
		{
			base.DisplayInfo(infoPanel);

			for (var i = 0; i < magicEffects.Length; i++)
			{
				var description = magicEffects[i].GetDescription(characterAttributes[i], characterSkills[i]);
				infoPanel.AddEffectIcon(magicEffects[i].ItemTexture, description);
			}
		}
	}
}