using System;
using UnityEngine;

public class SoundGenerator : EsmRecordCollection<SoundGenerator>
{
	const string DefaultSoundGeneratorName = "DEFAULT";

	[SerializeField]
	private CreatureRecord creature;

	[SerializeField]
	public SoundGeneratorType type;

	[SerializeField]
	private SoundRecord sound;

	public CreatureRecord Creature => creature;

	public static SoundGenerator Get(string creature, int index)
	{
		var key = $"{creature}{index:0000}";

		SoundGenerator record;
		if(!Records.TryGetValue(key, out record))
		{
			key = $"{DefaultSoundGeneratorName}{index:0000}";
			record = Records[key];
		}

		return record;
	}

	public override void Initialize(System.IO.BinaryReader reader, RecordHeader header)
	{
		while (reader.BaseStream.Position < header.DataEndPos)
		{
			var subRecordType = (SubRecordType)reader.ReadInt32();
			var size = reader.ReadInt32();

			switch (subRecordType)
			{
				case SubRecordType.Id:
					name = reader.ReadString(size);
					break;
				case SubRecordType.Data:
					type = (SoundGeneratorType)reader.ReadInt32();
					break;
				case SubRecordType.SoundName:
					sound = Record.GetRecord<SoundRecord>(reader.ReadString(size));
					break;
				case SubRecordType.CreatureName:
					creature = Record.GetRecord<CreatureRecord>(reader.ReadString(size));
					break;
				default:
					throw new NotImplementedException(subRecordType.ToString());
			}
		}
	}

	public void PlaySound(AudioSource audioSource)
	{
		sound.PlaySoundFromAudioSource(audioSource);
	}
}