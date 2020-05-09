using System;
using UnityEngine;

namespace Esm
{
	[Serializable]
	public class MiscItemRecordData : ItemRecordData
	{
		[SerializeField]
		private bool isKey;

		public MiscItemRecordData(System.IO.BinaryReader reader, string name)
		{
			weight = reader.ReadSingle();
			value = reader.ReadInt32();
			isKey = reader.ReadInt32() != 0;
		}
	}
}