using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

public static class DDSImporter
{
	public static Texture ReadFile(BinaryReader reader)
	{
		// Read and check the magic string (Should be 'DDS ', or  0x20534444)
		var magic = reader.ReadInt32();
		if(magic != 0x20534444)
		{
			throw new Exception("DDS file invalid");
		}

		// Read the header
		var headerSize = reader.ReadInt32();
		var headerFlags = (HeaderFlags)reader.ReadInt32();
		var height = reader.ReadInt32();
		var width = reader.ReadInt32();
		var pitchOrLinearSize = reader.ReadInt32();
		var depth = reader.ReadInt32();
		var mipMapCount = reader.ReadInt32();
		var dwReserved1 = reader.ReadBytes(sizeof(int) * 11);

		// Read pixel format
		var formatSize = reader.ReadInt32();
		var formatFlags = (PixelFormatFlags)reader.ReadInt32();
		var fourCC = Encoding.ASCII.GetString(reader.ReadBytes(4));
		var rgbBitCount = reader.ReadInt32();
		var redBitMask = reader.ReadInt32();
		var greenBitMask = reader.ReadInt32();
		var blueBitMask = reader.ReadInt32();
		var alphaBitMask = reader.ReadInt32();

		var Caps = (HeaderCaps)reader.ReadInt32();
		var Caps2 = reader.ReadInt32();
		var Caps3 = reader.ReadInt32();
		var Caps4 = reader.ReadInt32();
		var Reserved2 = reader.ReadInt32();

		// Get the size of the top level of the texture from the header, and read it
		var dataSize = formatFlags.HasFlag(PixelFormatFlags.FourCC) ? pitchOrLinearSize : pitchOrLinearSize * height;
		int missingDataSize;

		int bytesPerBlock;
		GraphicsFormat textureFormat;
		switch (formatFlags)
		{
			case (PixelFormatFlags.AlphaPixels | PixelFormatFlags.Rgb):
				bytesPerBlock = rgbBitCount;
				textureFormat = GraphicsFormat.B8G8R8A8_SRGB;
				break;
			case PixelFormatFlags.FourCC:
				switch (fourCC)
				{
					case "DXT1":
						bytesPerBlock = 8;
						textureFormat = GraphicsFormat.RGBA_DXT1_SRGB;
						break;
					case "DXT3":
						bytesPerBlock = 16;
						textureFormat = GraphicsFormat.RGBA_DXT3_SRGB;
						break;
					case "DXT5":
						bytesPerBlock = 16;
						textureFormat = GraphicsFormat.RGBA_DXT5_SRGB;
						break;
					default:
						throw new NotImplementedException(fourCC);
				}
				break;
			default:
				throw new NotImplementedException(formatFlags.ToString());
		}

		var hasMipMaps = Caps.HasFlag(HeaderCaps.Complex) || Caps.HasFlag(HeaderCaps.Mipmap);
		var data2Size = GetData2Size(width, height, bytesPerBlock, dataSize, mipMapCount, hasMipMaps, out missingDataSize);

		var data = new byte[dataSize + data2Size + missingDataSize];
		reader.Read(data, 0, dataSize + data2Size);

		var flags = hasMipMaps ? TextureCreationFlags.MipChain : TextureCreationFlags.None;
		var texture = new Texture2D(width, height, textureFormat, mipMapCount, flags);
		texture.LoadRawTextureData(data);
		texture.Apply(false, true);

		return texture;
	}

	private static int GetData2Size(int Width, int Height, int BytesPerBlock, int DataSize, int MipMapCount, bool HasMipMaps, out int missingDataSize)
	{
		// Just assume we're working with mipmaps for now
		var width = Width;
		var height = Height;
		var bytesPerBlock = BytesPerBlock;
		var topLevelSize = DataSize;
		var size = 0;

		if (width == height)
		{
			// Start at 1, as we don't want to take the top mip map into account
			for (var i = 1; i < MipMapCount; i++)
			{
				topLevelSize /= 4;
				size += topLevelSize;
			}
		}
		else
		{
			for (var i = 1; i < MipMapCount; i++)
			{
				// Since we're starting with the full width/height, halve them both first
				width /= 2;
				height /= 2;
				size += Math.Max(1, ((width + 3) / 4)) * Math.Max(1, ((height + 3) / 4)) * bytesPerBlock;
			}
		}

		// If mip maps exist, we need to give unity all sizes down to 1x1, so calculate the required size for the remaining data.
		missingDataSize = 0;
		if (HasMipMaps)
		{
			do
			{
				// Since we're starting with the full width/height, halve them both first
				width /= 2;
				height /= 2;

				if (width == height)
				{
					missingDataSize += width * height * bytesPerBlock;
				}
				else
				{
					missingDataSize += Math.Max(1, ((width + 3) / 4)) * Math.Max(1, ((height + 3) / 4)) * bytesPerBlock;
				}
			}
			while (Math.Max(width, height) > 1);
		}

		return size;
	}

	[Flags]
	private enum HeaderFlags
	{
		Caps = 0x1,
		Height = 0x2,
		Width = 0x4,
		Pitch = 0x8,
		PixelFormat = 0x1000,
		MipCount = 0x20000,
		LinearSize = 0x80000,
		Depth = 0x800000
	}

	[Flags]
	private enum HeaderCaps
	{
		Complex = 0x8,
		Texture = 0x1000,
		Mipmap = 0x400000
	}

	[Flags]
	private enum PixelFormatFlags
	{
		AlphaPixels = 0x1,
		Alpha = 0x2,
		FourCC = 0x4,
		Rgb = 0x40,
		Yuv = 0x200,
		Luminance = 0x20000
	}
}