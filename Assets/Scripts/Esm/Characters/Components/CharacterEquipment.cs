#pragma warning disable 0108

using System;
using System.Collections.Generic;
using Esm;
using UnityEngine;

public class CharacterEquipment : MonoBehaviour
{
	public event Action<WeaponType> OnWeaponChanged;

	private bool weaponEquipped;

	private CharacterAnimation animation;
	private CharacterBody body;

	private Dictionary<EquipmentSlot, IEquippable> equippedItems = new Dictionary<EquipmentSlot, IEquippable>();

	public WeaponRecord EquippedWeapon { get; private set; } //Should be inventoryItemData<WeaponData> or something

	public void Initialize(CharacterAnimation animation, CharacterBody body, IInventory inventory)
	{
		this.animation = animation;
		this.body = body;

		foreach (var item in inventory.Items)
		{
			var clothing = item.Key as IEquippable;
			if (clothing == null)
			{
				continue;
			}

			Equip(clothing, false);
		}
	}

	public WeaponType GetWeaponType()
	{
		if(EquippedWeapon == null)
		{
			return WeaponType.HandToHand;
		}

		return EquippedWeapon.Data.type;
	}

	// Called by the animation system
	public void Attach()
	{
		if(EquippedWeapon != null)
		{
			EquippedWeapon.PickupSound.PlaySoundAtPoint(transform.position);
			(EquippedWeapon as IEquippable).Equip(body.PartParts);
			animation.SetParameter("WeaponType", EquippedWeapon.Data.type);
		}

		weaponEquipped = true;
	}

	public void Detach()
	{
		if (EquippedWeapon != null)
		{
			EquippedWeapon.DropSound.PlaySoundAtPoint(transform.position);
			(EquippedWeapon as IEquippable).Unequip(body.PartParts);
		}

		animation.SetParameter("WeaponType", WeaponType.HandToHand);
		weaponEquipped = false;
	}

	public void EquipWeapon(WeaponRecord weapon,bool playSound = true)
	{
		// Play equip sound if needed (Don't want to play it when NPC's spawn for example)
		if (playSound)
		{
			weapon.PickupSound.PlaySoundAtPoint(transform.position);
		}

		// If a weapon is equipped, unequip it and equip the new one
		if (weaponEquipped)
		{
			// Unequip any existing weapons from the character's hands
			// Need this null check, as hand-to-hand could be equipped
			if(EquippedWeapon != null)
			{
				(EquippedWeapon as IEquippable).Unequip(body.PartParts);
			}
			
			(weapon as IEquippable).Equip(body.PartParts);
			OnWeaponChanged?.Invoke(weapon.Data.type);
		}

		EquippedWeapon = weapon;
		equippedItems[EquipmentSlot.Weapon] = weapon;
		animation.SetParameter("WeaponType", EquippedWeapon.Data.type);
	}

	public void Equip(IEquippable equipment, bool playSound = true)
	{
		// First, unequip any existing items if they are equipped
		IEquippable equippedItem;
		if(equippedItems.TryGetValue(equipment.EquipmentSlot, out equippedItem))
		{
			equippedItem.Unequip(body.PartParts);
		}

		// Now equip item
		equipment.Equip(body.PartParts);

		// Sometimes we don't want to play the equip sound, such as when npc's equip items
		if (playSound)
		{
			equipment.PickupSound.PlaySoundAtPoint(transform.position);
		}

		equippedItems[equipment.EquipmentSlot] = equipment;
		return;
	}

	public void UnequipWeapon()
	{
		if(EquippedWeapon == null)
		{
			return;
		}

		if (weaponEquipped)
		{
			(EquippedWeapon as IEquippable).Unequip(body.PartParts);
		}

		EquippedWeapon.DropSound.PlaySoundAtPoint(transform.position);
		equippedItems.Remove(EquipmentSlot.Weapon);
		EquippedWeapon = null;
		animation.SetParameter("WeaponType", WeaponType.HandToHand);
	}

	public void Unequip(IEquippable equipment)
	{
		IEquippable itemData;
		if(!equippedItems.TryGetValue(equipment.EquipmentSlot, out itemData))
		{
			return;
		}

		equipment.DropSound.PlaySoundAtPoint(transform.position);
		equipment.Unequip(body.PartParts);
		equippedItems.Remove(equipment.EquipmentSlot);
	}

	public bool IsEquipped(IEquippable equipment)
	{
		IEquippable itemData;
		return (equippedItems.TryGetValue(equipment.EquipmentSlot, out itemData) && 
			itemData == equipment);
	}
}