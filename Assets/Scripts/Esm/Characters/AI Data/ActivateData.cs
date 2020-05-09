using System;
using UnityEngine;

[Serializable]
public class ActivateData
{
	[SerializeField]
	private byte activateUnknown;

	[SerializeField]
	private string activateId;

	public ActivateData(System.IO.BinaryReader reader)
	{
		activateId = reader.ReadString(32);
		activateUnknown = reader.ReadByte();
	}

	public byte ActivateUnknown => activateUnknown;
	public string ActivateId => activateId;
}