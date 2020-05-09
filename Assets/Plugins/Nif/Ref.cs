using System.Collections.Generic;
using UnityEngine;

namespace Nif
{
	class Ref<T> where T : NiObject
	{
		protected int index;
		protected IReadOnlyList<NiObject> niObjects;

		public Ref(NiFile niFile)
		{
			index = niFile.Reader.ReadInt32();
			niObjects = niFile.NiObjects;
		}

		public T Target
		{
			get
			{
				if (index < 0)
				{
					return null;
				}

				return niObjects[index] as T;
			}
		}
	}
}