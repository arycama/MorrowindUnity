using UnityEngine;

public class ColorData
{
	public ColorData(System.IO.BinaryReader reader, int size)
	{
		Colors = reader.ReadColor323Array(65 * 65);
	}

	public Color32[] Colors { get; private set; }
}