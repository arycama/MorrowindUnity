using System;
using System.IO;
using UnityEngine;

public class Tes3Record : EsmRecord
{
	private long previousMasterFileSize;
	private string masterFileName;
	private Tes3Header tes3Header;

	public static Tes3Record Create(BinaryReader reader, RecordHeader header)
	{
		var instance = CreateInstance<Tes3Record>();
		instance.Initialize(reader, header);
		return instance;
	}

	public override void Initialize(BinaryReader reader, RecordHeader header)
	{
		while (reader.BaseStream.Position < header.DataEndPos)
		{
			var type = (SubRecordType)reader.ReadInt32();
			var size = reader.ReadInt32();

			switch (type)
			{
				case SubRecordType.Tes3Header:
					tes3Header = new Tes3Header(reader);
					break;
				case SubRecordType.MasterFile:
					masterFileName = reader.ReadString(size);
					break;
				case SubRecordType.Data:
					previousMasterFileSize = reader.ReadInt64();
					break;
			}
		}
	}

	[Serializable]
	private class Tes3Header
	{
		const int companyNameLength = 32;
		const int fileDescriptionLength = 256;

		private readonly float version;
		private readonly FileType fileType;
		public string companyName, fileDescription;
		private readonly int numberOfRecords;

		public Tes3Header(BinaryReader reader)
		{
			version = reader.ReadSingle();
			fileType = (FileType)reader.ReadInt32();
			companyName = reader.ReadString(companyNameLength);
			fileDescription = reader.ReadString(fileDescriptionLength);
			numberOfRecords = reader.ReadInt32();
		}

		private enum FileType
		{
			Esp = 0,
			Esm = 1,
			Ess = 32
		}
	}
}