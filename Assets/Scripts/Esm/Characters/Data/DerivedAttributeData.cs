using System;
using UnityEngine;

namespace Esm
{
	[Serializable]
	public class DerivedAttributeData
	{
		[SerializeField]
		private int health;

		[SerializeField]
		private int magicka;

		[SerializeField]
		private int fatigue;

		public DerivedAttributeData()
		{
			health = 1;
			magicka = 1;
			fatigue = 1;
		}

		public DerivedAttributeData(System.IO.BinaryReader reader)
		{
			health = reader.ReadInt16();
			magicka = reader.ReadInt16();
			fatigue = reader.ReadInt16();
		}

		public DerivedAttributeData(System.IO.BinaryReader reader, bool isLong = true)
		{
			health = reader.ReadInt32();
			magicka = reader.ReadInt32();
			fatigue = reader.ReadInt32();
		}

		public int Fatigue => fatigue;
		public int Health => health;
		public int Magicka => magicka;
	}
}