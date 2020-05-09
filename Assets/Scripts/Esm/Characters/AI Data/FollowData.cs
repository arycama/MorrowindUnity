using System;
using System.IO;
using UnityEngine;

[Serializable]
public class FollowData
{
	private Vector3 position;
	private int duration, unknown;
	private string id;

	public FollowData(System.IO.BinaryReader reader)
	{
		position = reader.ReadVector3();
		duration = reader.ReadInt16();
		id = reader.ReadString(32);
		unknown = reader.ReadInt16();
	}
}