using System;
using UnityEngine;

namespace Esm
{
	[Serializable]
	public class LightRecordData : ItemRecordData
	{
		public float Radius { get; private set; }
		public int Time { get; private set; }
		public byte NullByte { get; private set; }
		public LightFlags Flags { get; private set; }
		public Color32 Color { get; private set; }

		public LightRecordData(System.IO.BinaryReader reader)
		{
			weight = reader.ReadSingle();
			value = reader.ReadInt32();
			Time = reader.ReadInt32();
			Radius = reader.ReadInt32();
			Color = reader.ReadColor323();
			NullByte = reader.ReadByte();
			Flags = (LightFlags)reader.ReadInt32();
		}
	}
}