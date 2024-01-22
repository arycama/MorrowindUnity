using System;
using UnityEngine;

namespace Esm
{
	[Serializable]
	public class EnchantmentEffect
	{
		[SerializeField]
		private MagicEffectRecord magicEffect;

		private readonly int area, duration, minMagnitude, maxMagnitude;
		private readonly CharacterAttribute characterAttribute;
		private readonly RangeType rangeType;
		private readonly CharacterSkill characterSkill;

		public MagicEffectRecord MagicEffect { get { return magicEffect; } }

		public EnchantmentEffect(System.IO.BinaryReader reader)
		{
			magicEffect = MagicEffectRecord.Get((MagicEffectType)reader.ReadInt16());
			characterSkill = (CharacterSkill)reader.ReadByte();
			characterAttribute = (CharacterAttribute)reader.ReadByte();
			rangeType = (RangeType)reader.ReadInt32();
			area = reader.ReadInt32();
			duration = reader.ReadInt32();
			minMagnitude = reader.ReadInt32();
			maxMagnitude = reader.ReadInt32();
		}

		public void DisplayInfo(InfoPanel infoPanel)
		{
			var description = magicEffect.GetDescription(characterAttribute, characterSkill);
			if (minMagnitude == maxMagnitude)
			{
				infoPanel.AddEffectIcon(magicEffect.ItemTexture, $"{description} {minMagnitude} pts for {duration} secs");
			}
			else
			{
				infoPanel.AddEffectIcon(magicEffect.ItemTexture, $"{description} {minMagnitude} to {maxMagnitude} pts for {duration} secs");
			}
		}
	}
}