using System;
using System.IO;
using UnityEngine;

[Serializable]
public class TravelData
{
	private Vector3 position;
	private readonly int unknown;

	public TravelData(System.IO.BinaryReader reader)
	{
		position = reader.ReadVector3();
		unknown = reader.ReadInt32();
	}
}