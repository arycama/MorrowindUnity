using System;
using System.IO;

namespace Nif
{
	[Serializable]
	class NiUVController : NiTimeController
	{
		private readonly short unknownShort;
		private readonly int dataIndex;

		public NiUVController(NiFile niFile) : base(niFile)
		{
			unknownShort = niFile.Reader.ReadInt16();
			dataIndex = niFile.Reader.ReadInt32();
		}

		public override void Process()
		{
			// Don't set parent if there is no data attached
			if (dataIndex == -1)
			{
				return;
			}

			// Set the data object's parent to this object's parent
			var niObject = niFile.NiObjects[dataIndex];
			niObject.NiParent = NiParent;
		}

		public override void ProcessNiObject(NiObjectNet niObject)
		{
			if (dataIndex == -1)
			{
				return;
			}

			niFile.NiObjects[dataIndex].ProcessNiObject(niObject);
		}
	}
}