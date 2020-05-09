using System;
using UnityEngine;

namespace Esm
{
	[Serializable]
	public class RepairItemRecordData : ItemRecordData
	{
		[SerializeField]
		private float quality;

		[SerializeField]
		private int maxUses;

		public RepairItemRecordData(System.IO.BinaryReader reader)
		{
			weight = reader.ReadSingle();
			value = reader.ReadInt32();
			maxUses = reader.ReadInt32();
			quality = reader.ReadSingle();
		}

		public float Quality => quality;
		public int MaxUses => maxUses;

		public void DisplayInfo(InfoPanel infoPanel, int health)
		{
			infoPanel.AddText($"Uses: {health}");
			infoPanel.AddText($"Quality: {quality:F2}");

			base.DisplayInfo(infoPanel);
		}
	}
}