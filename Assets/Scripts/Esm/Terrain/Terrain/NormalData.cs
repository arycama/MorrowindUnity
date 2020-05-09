using System.Collections.Generic;
using UnityEngine;

// I think this is only used for the world map to represent lighting etc.
public class NormalData
{
	public sbyte[] Normals { get; private set; }

	public NormalData(System.IO.BinaryReader reader, int size)
	{
		Normals = reader.ReadSByteArray(size);
	}
}