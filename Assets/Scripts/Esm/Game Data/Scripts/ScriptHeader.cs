using System;
using UnityEngine;

namespace Esm
{
	[Serializable]
	public class ScriptHeader
	{
		[SerializeField]
		private int shortCount;

		[SerializeField]
		private int intCount;

		[SerializeField]
		private int floatCount;

		[SerializeField]
		private int scriptDataSize;

		[SerializeField]
		private int localVarSize;

		public string Name { get; private set; }

		public int ShortCount { get { return shortCount; } }
		public int IntCount { get { return intCount; } }
		public int FloatCount { get { return floatCount; } }
		public int ScriptDataSize { get { return scriptDataSize; } }
		public int LocalVarSize { get { return localVarSize; } }

		public ScriptHeader(System.IO.BinaryReader reader)
		{
			Name = reader.ReadString(32);
			shortCount = reader.ReadInt32();
			intCount = reader.ReadInt32();
			floatCount = reader.ReadInt32();
			scriptDataSize = reader.ReadInt32();
			localVarSize = reader.ReadInt32();
		}

		public int VariableCount
		{
			get
			{
				return shortCount + intCount + floatCount;
			}
		}
	}
}