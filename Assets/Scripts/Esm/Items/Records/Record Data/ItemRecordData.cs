using System;
using UnityEngine;

namespace Esm
{
	[Serializable]
	public abstract class ItemRecordData
	{
		[SerializeField]
		protected float weight;

		[SerializeField]
		protected int value;

		public int Value => value;
		public float Weight => weight;

		public virtual void DisplayInfo(InfoPanel infoPanel)
		{
			infoPanel.AddText($"Weight: {weight}");
			infoPanel.AddText($"Value: {value}");
		}
	}
}