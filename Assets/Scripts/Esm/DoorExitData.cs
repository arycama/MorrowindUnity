using System.IO;
using UnityEngine;

public class DoorExitData : ScriptableObject
{
	[SerializeField]
	private Vector3 position;

	[SerializeField]
	private Vector3 rotation;

	public Vector3 Position => position;

	public static DoorExitData Create(BinaryReader reader)
	{
		var data = CreateInstance<DoorExitData>();
		data.position = reader.ReadVector3();
		data.rotation = reader.ReadEulerAngle();
		return data;
	}

	public void OpenDoor(Transform target)
	{
		target.position = position;
		target.eulerAngles = rotation;
	}
}