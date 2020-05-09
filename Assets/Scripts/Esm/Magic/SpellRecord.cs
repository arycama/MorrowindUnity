using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Esm
{
	[Serializable]
	public class SpellRecord : EsmRecord
	{
		[SerializeField]
		private string fullName;

		[SerializeField]
		private List<EnchantmentEffect> effects = new List<EnchantmentEffect>();

		[SerializeField]
		private SpellData spellData;

		public string FullName => fullName;
		public SpellData Data => spellData;

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
					case SubRecordType.Name:
						fullName = reader.ReadString(size);
						break;
					case SubRecordType.SpellData:
						spellData = new SpellData(reader);
						break;
					case SubRecordType.Enchantment:
						effects.Add(new EnchantmentEffect(reader));
						break;
				}
			}
		}
	}
}