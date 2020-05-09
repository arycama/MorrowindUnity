using System;
using UnityEngine;

namespace Esm
{
	[Serializable]
	public class ApparatusRecordData : ItemRecordData
	{
		public float Quality { get; private set; }
		public ApparatusType Type { get; private set; }

		public ApparatusRecordData(System.IO.BinaryReader reader)
		{
			Type = (ApparatusType)reader.ReadInt32();
			Quality = reader.ReadSingle();
			weight = reader.ReadSingle();
			value = reader.ReadInt32();
		}

		public override void DisplayInfo(InfoPanel infoPanel)
		{
			infoPanel.AddText($"Quality: {Quality:F2}");
			base.DisplayInfo(infoPanel);
		}
	}
}