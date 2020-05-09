using System;
using UnityEngine;

namespace Esm
{
	public class ArmorRecordData : ItemRecordData
	{
		[SerializeField]
		private ArmorPart armorPart;

		[SerializeField]
		private int maxHealth;

		[SerializeField]
		private short enchantPoints;

		[SerializeField]
		private short unknown;

		[SerializeField]
		private int armorRating;

		public ArmorRecordData(System.IO.BinaryReader reader)
		{
			armorPart = (ArmorPart)reader.ReadInt32();
			weight = reader.ReadSingle();
			value = reader.ReadInt32();
			maxHealth = reader.ReadInt32();
			enchantPoints = reader.ReadInt16();
			unknown = reader.ReadInt16();
			armorRating = reader.ReadInt32();
		}

		public int MaxHealth => maxHealth;
		public short MaxCharge => enchantPoints;

		public EquipmentSlot EquipmentSlot
		{
			get
			{
				switch (armorPart)
				{
					case ArmorPart.Helmet:
						return EquipmentSlot.Helmet;
					case ArmorPart.Cuirass:
						return EquipmentSlot.Cuirass;
					case ArmorPart.LeftPauldron:
						return EquipmentSlot.LeftPauldron;
					case ArmorPart.RightPauldron:
						return EquipmentSlot.RightPauldron;
					case ArmorPart.Greaves:
						return EquipmentSlot.Greaves;
					case ArmorPart.Boots:
						return EquipmentSlot.Feet;
					case ArmorPart.LeftGauntlet:
					case ArmorPart.LeftBracer:
						return EquipmentSlot.LeftHand;
					case ArmorPart.RightGauntlet:
					case ArmorPart.RightBracer:
						return EquipmentSlot.RightHand;
					case ArmorPart.Shield:
						return EquipmentSlot.Shield;
					default:
						throw new NotImplementedException(armorPart.ToString());
				}
			}
		}

		public ArmorType ArmorType
		{
			get
			{
				int weightLimit = 1;
				switch (armorPart)
				{
					case ArmorPart.Helmet:
						weightLimit = GameSetting.Get("iHelmWeight").IntValue;
						break;
					case ArmorPart.Cuirass:
						weightLimit = GameSetting.Get("iCuirassWeight").IntValue;
						break;
					case ArmorPart.LeftPauldron:
					case ArmorPart.RightPauldron:
					case ArmorPart.LeftBracer:
					case ArmorPart.RightBracer:
						weightLimit = GameSetting.Get("iPauldronWeight").IntValue;
						break;
					case ArmorPart.Greaves:
						weightLimit = GameSetting.Get("iGreavesWeight").IntValue;
						break;
					case ArmorPart.Boots:
						weightLimit = GameSetting.Get("iBootsWeight").IntValue;
						break;
					case ArmorPart.LeftGauntlet:
					case ArmorPart.RightGauntlet:
						weightLimit = GameSetting.Get("iBootsWeight").IntValue;
						break;
					case ArmorPart.Shield:
						weightLimit = GameSetting.Get("iShieldWeight").IntValue;
						break;
				}

				var lightMaxMod =  GameSetting.Get("fLightMaxMod").FloatValue;
				const float epsilon = 5e-4f;
				if (weight <= weightLimit * lightMaxMod + epsilon)
				{
					return ArmorType.Light;
				}
				else
				{
					var medMaxMod =  GameSetting.Get("fMedMaxMod").FloatValue;
					if (weight <= weightLimit * medMaxMod + epsilon)
					{
						return ArmorType.Medium;
					}
					else
					{
						return ArmorType.Heavy;
					}
				}
			}
		}

		public SoundRecord DropSound
		{
			get
			{
				switch (ArmorType)
				{
					case ArmorType.Light:
						return Record.GetRecord<SoundRecord>("Item Armor Light Down");
					case ArmorType.Medium:
						return Record.GetRecord<SoundRecord>("Item Armor Medium Down");
					case ArmorType.Heavy:
						return Record.GetRecord<SoundRecord>("Item Armor Heavy Down");
					default:
						throw new NotImplementedException(ArmorType.ToString());
				}
			}
		}

		public SoundRecord PickupSound
		{
			get
			{
				switch (ArmorType)
				{
					case ArmorType.Light:
						return Record.GetRecord<SoundRecord>("Item Armor Light Up");
					case ArmorType.Medium:
						return Record.GetRecord<SoundRecord>("Item Armor Medium Up");
					case ArmorType.Heavy:
						return Record.GetRecord<SoundRecord>("Item Armor Heavy Up");
					default:
						throw new NotImplementedException(ArmorType.ToString());
				}
			}
		}

		public void DisplayInfo(InfoPanel infoPanel, int health)
		{
			infoPanel.AddText($"Armor Rating: {armorRating}");
			infoPanel.AddText($"Condition: {health}/{maxHealth}");
			infoPanel.AddText($"Weight: {weight} ({ArmorType})");
			infoPanel.AddText($"Value: {value}");
		}
	}
}