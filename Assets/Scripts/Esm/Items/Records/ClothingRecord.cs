using System;
using System.Collections.Generic;
using UnityEngine;

namespace Esm
{
	public class ClothingRecord : ItemRecord<ClothingRecordData>, IEquippable
	{
		[SerializeField]
		private EnchantmentData enchantment;

		[SerializeField]
		private ApparelRecordPiece[] clothingParts;

		public override SoundRecord PickupSound => data.PickupSound;
		public override SoundRecord DropSound => data.DropSound;

		EquipmentSlot IEquippable.EquipmentSlot => data.EquipmentSlot;

		public override GameObject CreateGameObject(ReferenceData referenceData, Transform parent = null)
		{
			var gameObject = base.CreateGameObject(referenceData, parent);
			var ownerData = new OwnerData(referenceData.Owner, referenceData.Global, referenceData.Faction, referenceData.Rank);

			var charge = referenceData.Charge == -1 ? data.MaxCharge : referenceData.Charge;
			Clothing.Create(gameObject, this, referenceData);

			return gameObject;
		}

		public override void Initialize(System.IO.BinaryReader reader, RecordHeader header)
		{
			var clothingParts = new List<ApparelRecordPiece>();

			while (reader.BaseStream.Position < header.DataEndPos)
			{
				var type = (SubRecordType)reader.ReadInt32();
				var size = reader.ReadInt32();

				switch (type)
				{
					case SubRecordType.Id:
						name = reader.ReadString(size);
						break;
					case SubRecordType.Model:
						model = reader.ReadString(size);
						break;
					case SubRecordType.Name:
						fullName = reader.ReadString(size);
						break;
					case SubRecordType.Script:
						script = Script.Get(reader.ReadString(size));
						break;
					case SubRecordType.ItemTexture:
						CreateSprite(reader.ReadString(size));
						break;
					case SubRecordType.Enchantment:
						enchantment = Record.GetRecord<EnchantmentData>(reader.ReadString(size));
						break;
					case SubRecordType.Index:
						clothingParts.Add(new ApparelRecordPiece());
						clothingParts[clothingParts.Count - 1].Index = (BipedPart)reader.ReadByte();
						break;
					case SubRecordType.BodyName:
						clothingParts[clothingParts.Count - 1].MalePart = BodyPartRecord.Get(reader.ReadString(size));
						break;
					case SubRecordType.CreatureName:
						clothingParts[clothingParts.Count - 1].FemalePart = BodyPartRecord.Get(reader.ReadString(size));
						break;
					case SubRecordType.ClothingData:
						data = new ClothingRecordData(reader);
						break;
				}
			}

			this.clothingParts = clothingParts.ToArray();
		}

		public InfoPanel DisplayInfo(Vector3 position, int quantity, bool displayIcon, float charge)
		{
			var infoPanel = base.CreateInfo(position, quantity, displayIcon);
			enchantment?.DisplayInfo(infoPanel, charge);
			return infoPanel;
		}

		public override void UseItem(GameObject target)
		{
			var equipment = target.GetComponent<CharacterEquipment>();
			if (equipment == null)
			{
				return;
			}

			if (equipment.IsEquipped(this))
			{
				equipment.Unequip(this);
			}
			else
			{
				equipment.Equip(this);
			}
		}

		void IEquippable.Equip(Dictionary<BipedPart, EquippedPart> bodyPartPairs)
		{
			foreach (var clothing in clothingParts)
			{
				var parent = bodyPartPairs[clothing.Index];

				if (clothing.MalePart != null)
				{
					// Need to find bodypart record first
					parent.Equip(clothing.MalePart.Model, this, clothing.Index);
				}
				else if (clothing.FemalePart != null)
				{
					parent.Equip(clothing.FemalePart.Model, this, clothing.Index);
				}
			}
		}

		void IEquippable.Unequip(Dictionary<BipedPart, EquippedPart> bodyPartPairs)
		{
			foreach (var clothing in clothingParts)
			{
				var parent = bodyPartPairs[clothing.Index];
				parent.Unequip(clothing.Index);
			}
		}
	}
}