using UnityEngine;

public class HeightData
{
	public float ReferenceHeight { get; private set; }
	public sbyte[] HeightPoints { get; private set; }

	public HeightData(System.IO.BinaryReader reader)
	{
		ReferenceHeight = reader.ReadSingle();
		HeightPoints = reader.ReadSByteArray(65 * 65);
		reader.ReadInt16(); // always -22801, or (239, 166)?
		reader.ReadSByte(); // always 0, deleted?
	}
}