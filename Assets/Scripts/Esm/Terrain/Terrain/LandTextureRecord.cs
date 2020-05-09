using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Esm
{
	class LandTextureRecord : EsmRecordCollection<int, LandTextureRecord>
	{
		private static Texture2DArray textureArray;

		private int index;
		private string id, texture;

		public override void Initialize(System.IO.BinaryReader reader, RecordHeader header)
		{
			while (reader.BaseStream.Position < header.DataEndPos)
			{
				var type = (SubRecordType)reader.ReadInt32();
				var size = reader.ReadInt32();

				switch (type)
				{
					case SubRecordType.Id:
						id = reader.ReadString(size);
						break;
					case SubRecordType.IntValue:
						index = reader.ReadInt32();
						break;
					case SubRecordType.Data:
						texture = reader.ReadString(size);
						break;
				}
			}

			records.Add(index, this);
		}

		public static Texture2D GetTexture(int index)
		{
			if (index - 1 < 0)
			{
				index = records.Count - 1;
			}
			else
			{
				index -= 1;
			}

			var record = records[index];
			var textureFilePath = record.texture;
			return BsaFileReader.LoadTexture("textures\\" + textureFilePath) as Texture2D;
		}

		public static Texture2DArray GetTexture2DArray()
		{
			if(textureArray != null)
				return textureArray;

			for(var i = 0; i < records.Count; i++)
			{
				var texture = GetTexture(i);
				if(textureArray == null)
					textureArray = new Texture2DArray(texture.width, texture.height, records.Count, texture.graphicsFormat, TextureCreationFlags.MipChain, texture.mipmapCount);

				// Some textures are null, so skip those
				if(texture == null)
					continue;

				Graphics.CopyTexture(texture, 0, textureArray, i);
			}

			records.Clear();
			return textureArray;
		}
	}
}