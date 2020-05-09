using System;
using System.Collections.Generic;
using UnityEngine;

namespace Esm
{
	[Serializable]
	public class MagicEffectRecord : EsmRecordCollection<MagicEffectType, MagicEffectRecord>
	{
		[SerializeField]
		private MagicEffectType index;

		[SerializeField, TextArea(5, 20)]
		private string description;

		public string areaVisual, boltVisual, castVisual, hitVisual, areaSound, boltSound, castSound, hitSound;

		[SerializeField]
		public MagicEffectData magicEffectData;

		[SerializeField]
		private Sprite itemTexture;

		[SerializeField]
		private Sprite particleTexture;

		public Sprite ItemTexture => itemTexture;

		//public static MagicEffectRecord GetMagicEffectRecord(MagicEffectType key)
		//{
		//	MagicEffectRecord record;
		//	if (Records.TryGetValue(key, out record))
		//	{
		//		return record;
		//	}

		//	RecordHeader header;
		//	if (!Record.MagicEffectOffsets.TryGetValue(key, out header))
		//	{
		//		throw new KeyNotFoundException(key.ToString());
		//	}

		//	var previousPosition = EsmFileReader.reader.Position;
		//	EsmFileReader.reader.Position = header.DataOffset;
		//	var dataEndPos = header.DataOffset + header.DataSize;
		//	var data = CreateInstance<MagicEffectRecord>();
		//	(data as IRecordData).Initialize(EsmFileReader.reader, header);
		//	EsmFileReader.reader.Position = previousPosition;
		//	return data;
		//}

		public override void Initialize(System.IO.BinaryReader reader, RecordHeader header)
		{
			while (reader.BaseStream.Position < header.DataEndPos)
			{
				var type = (SubRecordType)reader.ReadInt32();
				var size = reader.ReadInt32();

				switch (type)
				{
					case SubRecordType.Index:
						index = (MagicEffectType)reader.ReadInt32();
						name = index.ToString();
						break;
					case SubRecordType.MagicEffectData:
						magicEffectData = new MagicEffectData(reader);
						break;
					case SubRecordType.ItemTexture:
						{
							var texture = BsaFileReader.LoadTexture("icons/" + reader.ReadString(size)) as Texture2D;
							var rect = new Rect(0, 0, texture.width, texture.height);
							var pivot = new Vector2(0.5f, 0.5f);
							itemTexture = Sprite.Create(texture, rect, pivot);
							break;
						}
					case SubRecordType.ParticleTexture:
						{
							var texture = BsaFileReader.LoadTexture("textures/" + reader.ReadString(size)) as Texture2D;
							var rect = new Rect(0, 0, texture.width, texture.height);
							var pivot = new Vector2(0.5f, 0.5f);
							particleTexture = Sprite.Create(texture, rect, pivot);
							break;
						}
					case SubRecordType.MagicCastVisual:
						castVisual = reader.ReadString(size);
						break;
					case SubRecordType.MagicBoltVisual:
						boltVisual = reader.ReadString(size);
						break;
					case SubRecordType.MagicHitVisual:
						hitVisual = reader.ReadString(size);
						break;
					case SubRecordType.MagicAreaVisual:
						areaVisual = reader.ReadString(size);
						break;
					case SubRecordType.Description:
						description = reader.ReadString(size);
						break;
					case SubRecordType.MagicCastSound:
						castSound = reader.ReadString(size);
						break;
					case SubRecordType.MagicBoltsound:
						boltSound = reader.ReadString(size);
						break;
					case SubRecordType.MagicHitSound:
						hitSound = reader.ReadString(size);
						break;
					case SubRecordType.MagicAreaSound:
						areaSound = reader.ReadString(size);
						break;
				}
			}

			records.Add(index, this);
		}

		public string GetDescription(CharacterAttribute characterAttribute, CharacterSkill characterSkill)
		{
			var description = GameSetting.Get(index.ToString()).StringValue;
			switch (index)
			{
				case MagicEffectType.sEffectAbsorbAttribute:
				case MagicEffectType.sEffectDamageAttribute:
				case MagicEffectType.sEffectDrainAttribute:
				case MagicEffectType.sEffectFortifyAttribute:
				case MagicEffectType.sEffectRestoreAttribute:
					var attribute = GameSetting.Get(characterAttribute.ToString()).StringValue;
					description = description.Replace("Attribute", attribute);
					break;
				case MagicEffectType.sEffectAbsorbSkill:
				case MagicEffectType.sEffectDamageSkill:
				case MagicEffectType.sEffectDrainSkill:
				case MagicEffectType.sEffectFortifySkill:
				case MagicEffectType.sEffectRestoreSkill:
					var skill = GameSetting.Get(characterSkill.ToString()).StringValue;
					description = description.Replace("Skill", skill);
					break;
			}

			return description;
		}
	}
}