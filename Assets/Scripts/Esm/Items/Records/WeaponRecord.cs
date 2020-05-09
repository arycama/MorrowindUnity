using System.Collections.Generic;
using UnityEngine;

namespace Esm
{
	public class WeaponRecord : ItemRecord<WeaponRecordData>, IEquippable
	{
		[SerializeField]
		private EnchantmentData enchantment;

		public override SoundRecord PickupSound => data.PickupSound;
		public override SoundRecord DropSound => data.DropSound;

		EquipmentSlot IEquippable.EquipmentSlot => data.EquipmentSlot;

		public override void Initialize(System.IO.BinaryReader reader, RecordHeader header)
		{
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
					case SubRecordType.WeaponData:
						data = new WeaponRecordData(reader);
						break;
				}
			}
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
				equipment.UnequipWeapon();
			}
			else
			{
				equipment.EquipWeapon(this);
			}
		}

		public override GameObject CreateGameObject(ReferenceData referenceData, Transform parent = null)
		{
			var gameObject = base.CreateGameObject(referenceData, parent);

			// Some weapons have a skinned mesh such as bows and crossbows, so make sure these have visible collision meshes.
			var skinnedMeshRenderers = gameObject.GetComponentsInChildren<SkinnedMeshRenderer>();
			foreach (var skinnedMeshRenderer in skinnedMeshRenderers)
			{
				var meshCollider = gameObject.AddComponent<MeshCollider>();
				meshCollider.sharedMesh = skinnedMeshRenderer.sharedMesh;
			}

			var ownerData = new OwnerData(referenceData.Owner, referenceData.Global, referenceData.Faction, referenceData.Rank);

			var health = referenceData.Health == -1 ? data.MaxHealth : referenceData.Health;
			var charge = referenceData.Charge == -1 ? data.enchantPts : referenceData.Charge;

			Weapon.Create(gameObject, this, referenceData);

			return gameObject;
		}

		public InfoPanel DisplayInfo(Vector3 position, int quantity, bool displayIcon, int health, float charge)
		{
			var infoPanel = base.CreateInfo(position, quantity, displayIcon);
			data.DisplayInfo(infoPanel, health);
			enchantment?.DisplayInfo(infoPanel, charge);
			return infoPanel;
		}

		void IEquippable.Equip(Dictionary<BipedPart, EquippedPart> bodyPartPairs)
		{
			var parent = bodyPartPairs[BipedPart.Weapon];
			parent.Equip(model, this, BipedPart.Weapon);
		}

		void IEquippable.Unequip(Dictionary<BipedPart, EquippedPart> bodyPartPairs)
		{
			var parent = bodyPartPairs[BipedPart.Weapon];
			parent.Unequip(BipedPart.Weapon);
		}
	}
}