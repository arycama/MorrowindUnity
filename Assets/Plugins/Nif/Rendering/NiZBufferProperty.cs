using System;
using System.IO;
using UnityEngine.Rendering;

namespace Nif
{
	[Serializable]
	class NiZBufferProperty : NiProperty
	{
		private readonly ZBufferFlags flags;

		[Flags]
		private enum ZBufferFlags : short
		{
			Enabled = 0x0,
			ReadWrite = 0x1,
		};

		private enum CompareOp
		{
			Always,
			Less,
			Equal,
			LessOrEqual,
			Greater,
			NotEqual,
			GreaterOrEqual,
			Never
		}

		public NiZBufferProperty(NiFile niFile) : base(niFile)
		{
			flags = (ZBufferFlags)niFile.Reader.ReadInt16();
		}

		public override void ProcessNiObject(NiObjectNet niObject)
		{
			var zWrite = flags.HasFlag(ZBufferFlags.Enabled);
			niObject.Material.SetInt("_ZWrite", zWrite ? 1 : 0);

			var compareOp = (CompareOp)((int)flags >> 2);

			switch (compareOp)
			{
				case CompareOp.Always:
					niObject.Material.SetInt("_ZTest", (int)CompareFunction.Always);
					break;
				case CompareOp.Less:
					niObject.Material.SetInt("_ZTest", (int)CompareFunction.Less);
					break;
				case CompareOp.Equal:
					niObject.Material.SetInt("_ZTest", (int)CompareFunction.Equal);
					break;
				case CompareOp.LessOrEqual:
					niObject.Material.SetInt("_ZTest", (int)CompareFunction.Greater);
					break;
				case CompareOp.Greater:
					niObject.Material.SetInt("_ZTest", (int)CompareFunction.Greater);
					break;
				case CompareOp.NotEqual:
					niObject.Material.SetInt("_ZTest", (int)CompareFunction.NotEqual);
					break;
				case CompareOp.GreaterOrEqual:
					niObject.Material.SetInt("_ZTest", (int)CompareFunction.GreaterEqual);
					break;
				case CompareOp.Never:
					niObject.Material.SetInt("_ZTest", (int)CompareFunction.Never);
					break;
				default:
					niObject.Material.SetInt("_ZTest", (int)CompareFunction.Disabled);
					break;
			}
		}
	}
}