using System.IO;
using UnityEngine;

namespace Nif
{
	abstract class NiDynamicEffect : NiAVObject
	{
		int numAffectedNodeListPointers;
		int[] affectedNodeListPointers;

		public NiDynamicEffect(NiFile niFile) : base(niFile)
		{
			numAffectedNodeListPointers = niFile.Reader.ReadInt32();

			affectedNodeListPointers = new int[numAffectedNodeListPointers];
			for (int i = 0; i < affectedNodeListPointers.Length; i++)
			{
				affectedNodeListPointers[i] = niFile.Reader.ReadInt32();
			}
		}
	}
}