using System;
using System.Collections.Generic;
using UnityEngine;

namespace Esm
{
	[Serializable]
	public class ClothingRecordData : ItemRecordData
	{
		[SerializeField]
		private short enchantPoints;

		[SerializeField]
		private ClothingType clothingPart;

		public short MaxCharge => enchantPoints;

		public ClothingRecordData(System.IO.BinaryReader reader)
		{
			clothingPart = (ClothingType)reader.ReadInt32();
			weight = reader.ReadSingle();
			value = reader.ReadInt16();
			enchantPoints = reader.ReadInt16();
		}

		public EquipmentSlot EquipmentSlot
		{
			get
			{
				switch (clothingPart)
				{
					case ClothingType.Pants:
						return EquipmentSlot.Pants;
					case ClothingType.Shoes:
						return EquipmentSlot.Feet;
					case ClothingType.Shirt:
						return EquipmentSlot.Shirt;
					case ClothingType.Belt:
						return EquipmentSlot.Belt;
					case ClothingType.Robe:
						return EquipmentSlot.Robe;
					case ClothingType.RightGlove:
						return EquipmentSlot.RightHand;
					case ClothingType.LeftGlove:
						return EquipmentSlot.LeftHand;
					case ClothingType.Skirt:
						return EquipmentSlot.Skirt;
					case ClothingType.Ring:
						return EquipmentSlot.LeftRing; // Somehow make this work with both hands (Giggidy)
					case ClothingType.Amulet:
						return EquipmentSlot.Amulet;
					default:
						throw new NotImplementedException(clothingPart.ToString());
				}
			}
		}

		public SoundRecord DropSound
		{
			get
			{
				switch (clothingPart)
				{
					case ClothingType.Amulet:
					case ClothingType.Ring:
						return Record.GetRecord<SoundRecord>("Item Ring Down");
					default:
						return Record.GetRecord<SoundRecord>("Item Clothes Down");
				}
			}
		}

		public SoundRecord PickupSound
		{
			get
			{
				switch (clothingPart)
				{
					case ClothingType.Amulet:
					case ClothingType.Ring:
						return Record.GetRecord<SoundRecord>("Item Ring Up");
					default:
						return Record.GetRecord<SoundRecord>("Item Clothes Up");
				}
			}
		}
	}
}