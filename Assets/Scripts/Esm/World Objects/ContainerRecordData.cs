using System;
using System.IO;
using UnityEngine;

namespace Esm
{
	[Serializable]
	public class ContainerRecordData
	{
		[SerializeField]
		private float weight;

		public float Weight => weight;

		public ContainerRecordData(BinaryReader reader)
		{
			weight = reader.ReadSingle();
		}
	}
}