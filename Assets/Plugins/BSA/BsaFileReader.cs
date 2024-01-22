using System.Collections.Generic;
using System.IO;
using UnityEngine;

[ExecuteAlways]
public static class BsaFileReader
{
	private static readonly Dictionary<int, FileMetadata> fileCache;

	private static readonly string path = "C:/Program Files (x86)/Steam/steamapps/common/Morrowind/Data Files/Morrowind.bsa";
	private static BsaHeader header;

	private static readonly FileStream fileStream;
	private static readonly BinaryReader reader;

	static BsaFileReader()
	{
		fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
		reader = new BinaryReader(fileStream);

		// Read the header.
		header = new BsaHeader(reader);

		// Calculate some useful values.
		var hashTablePosition = 12 + header.HashOffset;
		var fileOffset = hashTablePosition + (8 * header.FileCount);

		// Create file metadatas.
		reader.BaseStream.Position = 12;
		var fileMetadatas = new FileMetadata[header.FileCount];
		for (var i = 0; i < header.FileCount; i++)
		{
			fileMetadatas[i] = new FileMetadata(reader, fileOffset);
		}

		// Read filename hashes.
		fileCache = new Dictionary<int, FileMetadata>(fileMetadatas.Length);
		reader.BaseStream.Position = header.HashOffset + 12;
		foreach (var metadata in fileMetadatas)
		{
			// Add the file metadata to the hash table
			var hash = reader.ReadInt64();
			fileCache.Add(hash.GetHashCode(), metadata);
		}
	}

	public static BinaryReader LoadArchiveFileData(string path)
	{
		var metadata = fileCache[HashFilePath(path)];
		reader.BaseStream.Position = metadata.Offset;
		return reader;
	}

	public static Texture LoadTexture(string path)
	{
		FileMetadata metadata;
		try
		{
			metadata = GetTextureMetadata(path);
		}
		catch (System.Exception)
		{
			return null;
		}

		var position = metadata.Offset;

		reader.BaseStream.Position = position;
		var texture = DDSImporter.ReadFile(reader);
		texture.name = path;
		return texture;
	}

	private static FileMetadata GetTextureMetadata(string path)
	{
		var textureName = Path.GetFileNameWithoutExtension(path);
		var textureNameInTexturesDir = textureName;

		FileMetadata metadata;
		var filePath = textureNameInTexturesDir + ".dds";
		if (fileCache.TryGetValue(HashFilePath(filePath), out metadata))
		{
			return metadata;
		}

		filePath = textureNameInTexturesDir + ".tga";
		if (fileCache.TryGetValue(HashFilePath(filePath), out metadata))
		{
			return metadata;
		}

		var texturePathWithoutExtension = Path.GetDirectoryName(path) + '\\' + textureName;

		filePath = texturePathWithoutExtension + ".dds";
		if (fileCache.TryGetValue(HashFilePath(filePath), out metadata))
		{
			return metadata;
		}

		filePath = texturePathWithoutExtension + ".tga";
		if (fileCache.TryGetValue(HashFilePath(filePath), out metadata))
		{
			return metadata;
		}

		// Could not find the file.
		throw new System.Exception("Could not find texture " + path);
	}

	public static char ToLowerFastIf(char c)
	{
		if (c >= 'A' && c <= 'Z')
		{
			return (char)(c + 32);
		}
		else
		{
			return c;
		}
	}

	private static int HashFilePath(string filePath)
	{
		uint len = (uint)filePath.Length;
		uint l = (len >> 1);
		int off, i;
		uint sum, temp, n;

		sum = 0;
		off = 0;

		for (i = 0; i < l; i++)
		{
			sum ^= (uint)(ToLowerFastIf(filePath[i])) << (off & 0x1F);
			off += 8;
		}

		var value1 = sum;

		sum = 0;
		off = 0;

		for (; i < len; i++)
		{
			temp = (uint)(ToLowerFastIf(filePath[i])) << (off & 0x1F);
			sum ^= temp;
			n = temp & 0x1F;
			sum = (sum << (32 - (int)n)) | (sum >> (int)n);
			off += 8;
		}

		return unchecked((int)(value1 ^ sum));
	}

	private struct BsaHeader
	{
		public int Version { get; private set; }
		public int HashOffset { get; private set; }
		public int FileCount { get; private set; }

		public BsaHeader(BinaryReader reader)
		{
			Version = reader.ReadInt32();
			HashOffset = reader.ReadInt32();
			FileCount = reader.ReadInt32();
		}
	}

	private struct FileMetadata
	{
		public int Size { get; private set; }
		public int Offset { get; private set; }

		public FileMetadata(BinaryReader reader, int offset)
		{
			Size = reader.ReadInt32();
			Offset = reader.ReadInt32() + offset;
		}
	}
}