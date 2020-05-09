using System;
using System.Collections.Generic;

namespace Esm
{
	[Serializable]
	public class WeaponRecordData : ItemRecordData
	{
		public byte chopMax;
		public byte chopMin;
		public byte slashMax;
		public byte slashMin;
		public byte thrustMax;
		public byte thrustMin;
		public float reach;
		public float Speed;
		public short enchantPts;
		private short maxHealth;

		public WeaponFlags WeaponFlags;
		public WeaponType type;

		public WeaponRecordData(System.IO.BinaryReader reader)
		{
			weight = reader.ReadSingle();
			value = reader.ReadInt32();
			type = (WeaponType)reader.ReadInt16();
			maxHealth = reader.ReadInt16();
			Speed = reader.ReadSingle();
			reach = reader.ReadSingle();
			enchantPts = reader.ReadInt16();
			chopMin = reader.ReadByte();
			chopMax = reader.ReadByte();
			slashMin = reader.ReadByte();
			slashMax = reader.ReadByte();
			thrustMin = reader.ReadByte();
			thrustMax = reader.ReadByte();
			WeaponFlags = (WeaponFlags)reader.ReadInt32();
		}

		public short MaxHealth => maxHealth;

		public EquipmentSlot EquipmentSlot
		{
			get
			{
				switch (type)
				{
					case WeaponType.Arrow:
					case WeaponType.Bolt:
						return EquipmentSlot.Ammo;
					default:
						return EquipmentSlot.Weapon;
				}
			}
		}

		public SoundRecord DropSound
		{
			get
			{
				switch (type)
				{
					case WeaponType.ShortBladeOneHand:
						return Record.GetRecord<SoundRecord>("Item Weapon Shortblade Down");
					case WeaponType.LongBladeOneHand:
					case WeaponType.LongBladeTwoClose:
						return Record.GetRecord<SoundRecord>("Item Weapon Longblade Down");
					case WeaponType.SpearTwoWide:
						return Record.GetRecord<SoundRecord>("Item Weapon Spear Down");
					case WeaponType.MarksmanBow:
						return Record.GetRecord<SoundRecord>("Item Weapon Bow Down");
					case WeaponType.MarksmanCrossbow:
						return Record.GetRecord<SoundRecord>("Item Weapon Crossbow Down");
					case WeaponType.MarksmanThrown:
					case WeaponType.Arrow:
					case WeaponType.Bolt:
						return Record.GetRecord<SoundRecord>("Item Ammo Down");
					default:
						return Record.GetRecord<SoundRecord>("Item Weapon Blunt Down");
				}
			}
		}

		public SoundRecord PickupSound
		{
			get
			{
				switch (type)
				{
					case WeaponType.ShortBladeOneHand:
						return Record.GetRecord<SoundRecord>("Item Weapon Shortblade Up");
					case WeaponType.LongBladeOneHand:
					case WeaponType.LongBladeTwoClose:
						return Record.GetRecord<SoundRecord>("Item Weapon Longblade Up");
					case WeaponType.SpearTwoWide:
						return Record.GetRecord<SoundRecord>("Item Weapon Spear Up");
					case WeaponType.MarksmanBow:
						return Record.GetRecord<SoundRecord>("Item Weapon Bow Up");
					case WeaponType.MarksmanCrossbow:
						return Record.GetRecord<SoundRecord>("Item Weapon Crossbow Up");
					case WeaponType.MarksmanThrown:
					case WeaponType.Arrow:
					case WeaponType.Bolt:
						return Record.GetRecord<SoundRecord>("Item Ammo Up");
					default:
						return Record.GetRecord<SoundRecord>("Item Weapon Blunt Up");
				}
			}
		}

		public void DisplayInfo(InfoPanel infoPanel, int health)
		{
			infoPanel.AddText($"Type: {type}"); 
			infoPanel.AddText($"Chop: {chopMin} - {chopMax}");
			infoPanel.AddText($"Slash: {slashMin} - {slashMax}");
			infoPanel.AddText($"Thrust: {thrustMin} - {thrustMax}");
			infoPanel.AddText($"Condition: {health}/{maxHealth}");
			base.DisplayInfo(infoPanel);
		}
	}
}