using System;
using System.Collections.Generic;
using UnityEngine;
using Esm;

public class CreatureRecord : AIRecord
{
	[SerializeField]
	private string soundGeneratorName;

	[SerializeField, EnumFlags]
	private CreatureFlags flags;

	[SerializeField]
	private CreatureData data;

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
				case SubRecordType.AiData:
					aiData = new AiData(reader);
					break;
				case SubRecordType.AiActivateData:
					activateData = new ActivateData(reader);
					break;
				case SubRecordType.AiEscortData:
					escortData = new EscortData(reader);
					break;
				case SubRecordType.AiFollowData:
					followData = new FollowData(reader);
					break;
				case SubRecordType.AiTravelData:
					travelData = new TravelData(reader);
					break;
				case SubRecordType.AiWanderData:
					wanderData = new WanderData(reader);
					break;
				case SubRecordType.CreatureName:
					soundGeneratorName = reader.ReadString(size);
					break;
				case SubRecordType.InventoryItem:
					items.Add(new InventoryItem(reader));
					break;
				case SubRecordType.NpcSpell:
					spells.Add(Record.GetRecord<SpellRecord>(reader.ReadString(size)));
					break;
				case SubRecordType.Scale:
					scale = reader.ReadSingle();
					break;
				case SubRecordType.NpcData:
					data = new CreatureData(reader);
					break;
				case SubRecordType.Flag:
					flags = (CreatureFlags)reader.ReadInt32();
					break;
			}
		}

		// Use the record id for sound generation if no SoundGeneratorName is specified
		if (soundGeneratorName == null)
		{
			soundGeneratorName = name;
		}
	}

	public override GameObject CreateGameObject(ReferenceData referenceData, Transform parent = null)
	{
		var gameObject = base.CreateGameObject(referenceData, parent);
		if (flags.HasFlag(CreatureFlags.Flies))
		{
			var rigidbody = gameObject.GetComponent<Rigidbody>();
            rigidbody.useGravity = false;
		}

		CharacterAudio.Create(gameObject, soundGeneratorName);
		return gameObject;
	}

	protected override CharacterInput AddCharacterInput(GameObject gameObject)
	{
		var input = gameObject.AddComponent<CreatureInput>();
		input.CreatureFlags = flags;
		return input;
	}
}	