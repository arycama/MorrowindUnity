using System.IO;
using UnityEngine;

public class GameSetting : EsmRecordCollection<GameSetting>
{
	[SerializeField]
	private float floatValue;

	[SerializeField]
	private int intValue;

	[SerializeField]
	private string stringValue;

	public float FloatValue => floatValue;
	public int IntValue => intValue;
	public string StringValue => stringValue;

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
				case SubRecordType.StringValue:
					stringValue = reader.ReadString(size);
					break;
				case SubRecordType.IntValue:
					intValue = reader.ReadInt32();
					break;
				case SubRecordType.FloatValue:
					floatValue = reader.ReadSingle();
					break;
			}
		}
	}
}