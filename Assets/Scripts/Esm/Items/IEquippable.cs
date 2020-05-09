using System.Collections.Generic;
using Esm;

public interface IEquippable
{
	EquipmentSlot EquipmentSlot { get; }

	SoundRecord PickupSound { get; }
	SoundRecord DropSound { get; }

	void Equip(Dictionary<BipedPart, EquippedPart> bodyPartPairs);
	void Unequip(Dictionary<BipedPart, EquippedPart> bodyPartPairs);
}