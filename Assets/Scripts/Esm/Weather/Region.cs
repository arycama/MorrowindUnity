using System.Collections.Generic;
using Esm;
using UnityEngine;

public class Region : EsmRecord
{
	[SerializeField]
	private string fullName;

	[SerializeField]
	private LeveledCreatureRecord sleepCreature;

	[SerializeField]
	private Color32 mapColor;

	[SerializeField]
	private List<SoundName> sounds = new List<SoundName>();

	[SerializeField]
	private WeatherData weatherData;

	public string Name => fullName;
	public WeatherData WeatherData => weatherData;
	public IReadOnlyList<SoundName> Sounds => sounds;

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
				case SubRecordType.Name:
					fullName = reader.ReadString(size);
					break;
				case SubRecordType.WeatherData:
					weatherData = new WeatherData(reader, size);
					break;
				case SubRecordType.BodyName:
					sleepCreature = Record.GetRecord<LeveledCreatureRecord>(reader.ReadString(size));
					break;
				case SubRecordType.CreatureName:
					mapColor = reader.ReadColor32();
					break;
				case SubRecordType.SoundName:
					sounds.Add(new SoundName(reader, size));
					break;
			}
		}
	}
}