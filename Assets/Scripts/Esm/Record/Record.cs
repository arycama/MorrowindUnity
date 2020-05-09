using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Esm;

public static class Record
{
	private static Dictionary<string, EsmRecord> Records = new Dictionary<string, EsmRecord>(StringComparer.OrdinalIgnoreCase);

	public static void Create(BinaryReader reader)
	{
		var header = new RecordHeader(reader);

		switch (header.Type)
		{
			case RecordType.BirthSign:
				BirthSignRecord.Create(reader, header);
				break;
			case RecordType.BodyPart:
				BodyPartRecord.Create(reader, header);
				break;
			case RecordType.Cell:
				CellRecord.Create(reader, header);
				break;
			case RecordType.Dialogue:
				DialogRecord.Create(reader, header);
				break;
			case RecordType.GameSetting:
				GameSetting.Create(reader, header);
				break;
			case RecordType.Info:
				InfoRecord.Create(reader, header);
				break;
			case RecordType.Land:
				LandRecord.Create(reader, header);
				break;
			case RecordType.LandTexture:
				LandTextureRecord.Create(reader, header);
				break;
			case RecordType.MagicEffect:
				MagicEffectRecord.Create(reader, header);
				break;
			case RecordType.PathGrid:
				Pathgrid.Create(reader, header);
				break;
			case RecordType.Script:
				Script.Create(reader, header);
				break;
			case RecordType.Skill:
				SkillRecord.Create(reader, header);
				break;
			case RecordType.SoundGenerator:
				SoundGenerator.Create(reader, header);
				break;
			case RecordType.Tes3:
				Tes3Record.Create(reader, header);
				break;
			default:
				{
					var size = GotoSubrecord(SubRecordType.Id, header);
					var id = reader.ReadString(size);
					reader.BaseStream.Position = header.DataOffset + header.DataSize;
					var recordData = CreateRecordData(header.Type);
					recordData.Header = header;
					Records.Add(id, recordData);
					break;
				}
		}
	}

	public static T GetRecord<T>(string key) where T : EsmRecord
	{
		// First try to get the data from the cache
		var recordData = Records[key];
		if (!recordData.IsInitialized)
		{
			// Save the current reader position so it can be reset later
			var previousPosition = EsmFileReader.reader.BaseStream.Position;

			// Set the position to the record's start position
			EsmFileReader.reader.BaseStream.Position = recordData.Header.DataOffset;

			// Read the record
			recordData.Initialize(EsmFileReader.reader, recordData.Header);

			// Reset the reader position
			EsmFileReader.reader.BaseStream.Position = previousPosition;

			recordData.IsInitialized = true;
		}

		return (T)recordData;
	}

	private static int GotoSubrecord(SubRecordType targetType, RecordHeader header)
	{
		var reader = EsmFileReader.reader;
		while (reader.BaseStream.Position < header.DataEndPos)
		{
			var type = (SubRecordType)reader.ReadInt32();
			var size = reader.ReadInt32();

			if (type == targetType)
			{
				return size;
			}
				
			reader.BaseStream.Position += size + 8;
		}

		throw new Exception($"Sub-Record type [{targetType}] not found.");
	}

	private static EsmRecord CreateRecordData(RecordType type)
	{
		switch (type)
		{
			case RecordType.Activator:
				return ScriptableObject.CreateInstance<ActivatorRecord>();
			case RecordType.Alchemy:
				return ScriptableObject.CreateInstance<AlchemyRecord>();
			case RecordType.Apparatus:
				return ScriptableObject.CreateInstance<ApparatusRecord>();
			case RecordType.Armor:
				return ScriptableObject.CreateInstance<ArmorRecord>();
			case RecordType.Book:
				return ScriptableObject.CreateInstance<BookRecord>();
			case RecordType.Container:
				return ScriptableObject.CreateInstance<ContainerRecord>();
			case RecordType.Class:
				return ScriptableObject.CreateInstance<ClassRecord>();
			case RecordType.Clothing:
				return ScriptableObject.CreateInstance<ClothingRecord>();
			case RecordType.Creature:
				return ScriptableObject.CreateInstance<CreatureRecord>();
			case RecordType.Door:
				return ScriptableObject.CreateInstance<DoorRecord>();
			case RecordType.Enchantment:
				return ScriptableObject.CreateInstance<EnchantmentData>();
			case RecordType.Faction:
				return ScriptableObject.CreateInstance<Faction>();
			case RecordType.Global:
				return ScriptableObject.CreateInstance<Global>();
			case RecordType.Ingredient:
				return ScriptableObject.CreateInstance<IngredientRecord>();
			case RecordType.LevelledCreature:
				return ScriptableObject.CreateInstance<LeveledCreatureRecord>();
			case RecordType.LevelledItem:
				return ScriptableObject.CreateInstance<LeveledItemRecord>();
			case RecordType.Light:
				return ScriptableObject.CreateInstance<LightRecord>();
			case RecordType.Lockpick:
				return ScriptableObject.CreateInstance<LockpickRecord>();
			case RecordType.MiscItem:
				return ScriptableObject.CreateInstance<MiscItemRecord>();
			case RecordType.Npc:
				return ScriptableObject.CreateInstance<NpcRecord>();
			case RecordType.Probe:
				return ScriptableObject.CreateInstance<ProbeRecord>();
			case RecordType.Race:
				return ScriptableObject.CreateInstance<Race>();
			case RecordType.Region:
				return ScriptableObject.CreateInstance<Region>();
			case RecordType.Repair:
				return ScriptableObject.CreateInstance<RepairItemRecord>();
			case RecordType.Sound:
				return ScriptableObject.CreateInstance<SoundRecord>();
			case RecordType.Spell:
				return ScriptableObject.CreateInstance<SpellRecord>();
			case RecordType.Static:
				return ScriptableObject.CreateInstance<StaticRecord>();
			case RecordType.Weapon:
				return ScriptableObject.CreateInstance<WeaponRecord>();
			default:
				throw new NotImplementedException(type.ToString());
		}
	}
}