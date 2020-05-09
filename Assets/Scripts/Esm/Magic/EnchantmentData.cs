using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Esm
{
	public class EnchantmentData : EsmRecord
	{
		[SerializeField]
		private EnchantmentRecordData data;

		[SerializeField]
		private List<EnchantmentEffect> effects = new List<EnchantmentEffect>();

		public EnchantmentRecordData Data => data;
		public IReadOnlyList<EnchantmentEffect> Effects => effects;

		public override void Initialize(BinaryReader reader, RecordHeader header)
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
					case SubRecordType.EnchantmentData:
						data = new EnchantmentRecordData(reader);
						break;
					case SubRecordType.Enchantment:
						effects.Add(new EnchantmentEffect(reader));
						break;
				}
			}
		}

		public void DisplayInfo(InfoPanel infoPanel, float charge)
		{
			Data.DisplayInfo(infoPanel, Effects, charge);
		}
	}

	public enum CastType
	{
		sItemCastOnce,
		sItemCastWhenStrikes,
		sItemCastWhenUsed,
		sItemCastConstant
	};
}