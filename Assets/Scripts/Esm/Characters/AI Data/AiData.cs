using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class AiData
{
	[SerializeField]
	private byte hello;

	private readonly int unknown1, fight, flee, alarm, unknown2, unknown3, unknown4;

	[SerializeField, EnumFlags]
	private ServiceFlags buySellFlags;

	public AiData()
	{

	}

	public AiData(System.IO.BinaryReader reader)
	{
		hello = reader.ReadByte();
		unknown1 = reader.ReadByte();
		fight = reader.ReadByte();
		flee = reader.ReadByte();
		alarm = reader.ReadByte();
		unknown2 = reader.ReadByte();
		unknown3 = reader.ReadByte();
		unknown4 = reader.ReadByte();
		buySellFlags = (ServiceFlags)reader.ReadInt32();
	}

	public List<Service> GetServices()
	{
		var services = new List<Service>
			{
				Service.Persuasion
			};

		if (buySellFlags != ServiceFlags.None)
		{
			// Bitmask to get rid of all non-selling flags
			if (((int)buySellFlags & 0x37ff) != 0)
			{
				services.Add(Service.Barter);
			}

			if (buySellFlags.HasFlag(ServiceFlags.Spells))
			{
				services.Add(Service.Spells);
			}

			if (buySellFlags.HasFlag(ServiceFlags.Training))
			{
				services.Add(Service.Training);
			}

			if (buySellFlags.HasFlag(ServiceFlags.Spellmaking))
			{
				services.Add(Service.Spellmaking);
			}

			if (buySellFlags.HasFlag(ServiceFlags.Enchanting))
			{
				services.Add(Service.Enchanting);
			}

			if (buySellFlags.HasFlag(ServiceFlags.Repair))
			{
				services.Add(Service.Repair);
			}
		}

		return services;
	}

	public byte Hello => hello;
}