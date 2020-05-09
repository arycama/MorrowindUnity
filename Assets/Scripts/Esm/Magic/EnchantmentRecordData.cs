using System.Collections.Generic;

namespace Esm
{
	public class EnchantmentRecordData
	{
		public CastType CastType { get; private set; }
		public bool AutoCalculate { get; private set; }
		public int EnchantmentCost { get; private set; }
		public int ChargeAmount { get; private set; }

		public EnchantmentRecordData(System.IO.BinaryReader reader)
		{
			CastType = (CastType)reader.ReadInt32();
			EnchantmentCost = reader.ReadInt32();
			ChargeAmount = reader.ReadInt32();
			AutoCalculate = (reader.ReadInt32() == 0);
		}

		public void DisplayInfo(InfoPanel infoPanel, IEnumerable<EnchantmentEffect> effects, float charge)
		{
			// Display cast type
			var text = GameSetting.Get(CastType.ToString()).StringValue;
			infoPanel.AddText(text);

			foreach(var effect in effects)
			{
				effect.DisplayInfo(infoPanel);
			}

			infoPanel.AddCharge(charge, ChargeAmount);
		}
	}
}