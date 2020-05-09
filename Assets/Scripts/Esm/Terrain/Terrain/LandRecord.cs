using System;
using System.Collections.Generic;
using UnityEngine;

namespace Esm
{
	public class LandRecord : EsmRecordCollection<Vector2Int, LandRecord>
	{
		public LandFlags LandFlags { get; private set; }
		public int Data { get; private set; }
		public Vector2Int Position { get; private set; }
		public HeightData HeightData { get; private set; }
		public TextureData TextureData { get; private set; }
		public NormalData NormalData { get; private set; }
		public ColorData ColorData { get; private set; }
		public WnamData WnamData { get; private set; }

		public string Id { get; private set; }

		public override void Initialize(System.IO.BinaryReader reader, RecordHeader header)
		{
			while (reader.BaseStream.Position < header.DataEndPos)
			{
				var type = (SubRecordType)reader.ReadInt32();
				var size = reader.ReadInt32();

				switch (type)
				{
					case SubRecordType.IntValue:
						Position = new Vector2Int(reader.ReadInt32(), reader.ReadInt32());
						break;
					case SubRecordType.Data:
						LandFlags = (LandFlags)reader.ReadInt32();
						break;
					case SubRecordType.VertexNormalData:
						NormalData = new NormalData(reader, size);
						break;
					case SubRecordType.VertexHeightData:
						HeightData = new HeightData(reader);
						break;
					case SubRecordType.WnamData:
						WnamData = new WnamData(reader, size);
						break;
					case SubRecordType.VertexColorData:
						ColorData = new ColorData(reader, size);
						break;
					case SubRecordType.VertexTextureData:
						TextureData = new TextureData(reader, size);
						break;
				}
			}

			records.Add(Position, this);
		}
	}
}