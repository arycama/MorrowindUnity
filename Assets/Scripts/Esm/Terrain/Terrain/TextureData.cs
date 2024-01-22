using System.Collections.Generic;
using UnityEngine;

public class TextureData
{
	private readonly short[,] textureIndices;

	public TextureData(System.IO.BinaryReader reader, int size)
	{
		// Each index is an int16, so the size is the record size in bytes divided by two
		textureIndices = new short[16, 16];
		for (var y = 0; y < 16; y++)
		{
			for (var x = 0; x < 16; x++)
			{
				// These are stored in 4x4 blocks, left to right, bottom to top
				// Read them into a standard left-right, bottom-top array
				// Todo: simplify this, either using loop variables or algebra
				var xIndex = x % 4 + (y % 4) * 4;
				var yIndex = x / 4 + y / 4 * 4;

				textureIndices[xIndex, yIndex] = reader.ReadInt16();
			}
		}
	}

	public short[,] TextureIndices { get { return textureIndices; } }
}