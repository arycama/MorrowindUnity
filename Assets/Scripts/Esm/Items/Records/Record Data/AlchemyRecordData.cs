using System;
using UnityEngine;

namespace Esm
{
	[Serializable]
	public class AlchemyRecordData : ItemRecordData
	{
		[SerializeField]
		private int autoCalc;

		public AlchemyRecordData(System.IO.BinaryReader reader)
		{
			weight = reader.ReadSingle();
			value = reader.ReadInt32();
			autoCalc = reader.ReadInt32();
		}
	}
}