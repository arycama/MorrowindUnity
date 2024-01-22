using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public class CellRecord : EsmRecord
{
	private static readonly Dictionary<string, RecordHeader> interiorCellHeaders = new Dictionary<string, RecordHeader>();
	private static readonly Dictionary<Vector2Int, RecordHeader> exteriorCellHeaders = new Dictionary<Vector2Int, RecordHeader>();

	private static readonly Dictionary<string, CellRecord> interiorCells = new Dictionary<string, CellRecord>();
	private static readonly Dictionary<Vector2Int, CellRecord> exteriorCells = new Dictionary<Vector2Int, CellRecord>();

	[SerializeField]
	private Region region;

	[SerializeField]
	private float waterHeight;

	[SerializeField]
	private int objectCount;

	[SerializeField]
	private Color32 mapColor;

	[SerializeField]
	private AmbientData ambientData;

	[SerializeField]
	private CellData cellData;

	[SerializeField]
	private List<ReferenceData> referenceData = new List<ReferenceData>();

	public int ObjectCount => objectCount;
	public string Name => name;
	public Region Region => region;
	public float WaterHeight => waterHeight;
	public Color32 MapColor => mapColor;
	public AmbientData AmbientData => ambientData;
	public CellData CellData => cellData;
	public List<ReferenceData> ReferenceData => referenceData;

	public static void Create(BinaryReader reader, RecordHeader header)
	{
		var instance = CreateInstance<CellRecord>();
		instance.Initialize(reader, header);
	}

	public static CellRecord GetInteriorCell(string name)
	{
		CellRecord record;
		if(interiorCells.TryGetValue(name, out record))
		{
			return record;
		}

		RecordHeader header = interiorCellHeaders[name];

		// Save the current reader position so it can be reset later
		var prevoiusPosition = EsmFileReader.reader.BaseStream.Position;

		// Read the record
		EsmFileReader.reader.BaseStream.Position = header.DataOffset;
		var dataEndPos = header.DataOffset + header.DataSize;

		var cellRecord = CreateInstance<CellRecord>();
		cellRecord.Deserialize(EsmFileReader.reader, header);
			
		interiorCells.Add(name, cellRecord);

		// Reset the reader position
		EsmFileReader.reader.BaseStream.Position = prevoiusPosition;

		return cellRecord;
	}

	public static CellRecord GetExteriorCell(Vector2Int coordinates)
	{
		CellRecord record;
		if (exteriorCells.TryGetValue(coordinates, out record))
		{
			return record;
		}

		RecordHeader header = exteriorCellHeaders[coordinates];

		// Save the current reader position so it can be reset later
		var prevoiusPosition = EsmFileReader.reader.BaseStream.Position;

		// Read the record
		EsmFileReader.reader.BaseStream.Position = header.DataOffset;
		var dataEndPos = header.DataOffset + header.DataSize;

		var cellRecord = CreateInstance<CellRecord>();
		cellRecord.Deserialize(EsmFileReader.reader, header);

		exteriorCells.Add(coordinates, cellRecord);

		// Reset the reader position
		EsmFileReader.reader.BaseStream.Position = prevoiusPosition;

		return cellRecord;
	}

	public override void Initialize(BinaryReader reader, RecordHeader header)
	{
		string name = null;
		CellData cellData = null;

		while (reader.BaseStream.Position < header.DataEndPos)
		{
			var type = (SubRecordType)reader.ReadInt32();
			var size = reader.ReadInt32();

			if (type == SubRecordType.Id)
			{
				name = reader.ReadString(size);
			}
			else if(type == SubRecordType.Data)
			{
				cellData = new CellData(reader);
				break;
			}
		}

		if (cellData.IsInterior)
			interiorCellHeaders.Add(name, header);
		else
			exteriorCellHeaders.Add(cellData.Coordinates, header);

		reader.BaseStream.Position = header.DataEndPos + 4;
	}

	public override void Deserialize(BinaryReader reader, RecordHeader header)
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
				case SubRecordType.Data:
					cellData = new CellData(reader);
					break;
				case SubRecordType.RegionName:
					region = Record.GetRecord<Region>(reader.ReadString(size));
					break;
				case SubRecordType.Name0:
					objectCount = reader.ReadInt32();
					break;
				case SubRecordType.Name5:
					mapColor = reader.ReadColor32();
					break;
				case SubRecordType.AmbientData:
					ambientData = new AmbientData(reader);
					break;
				case SubRecordType.ReferenceData:
					referenceData.Add(new ReferenceData(reader));
					break;
				case SubRecordType.IntValue:
					waterHeight = reader.ReadInt32();
					break;
			}
		}
	}
}