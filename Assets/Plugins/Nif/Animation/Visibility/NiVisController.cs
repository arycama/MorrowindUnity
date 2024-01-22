using System;

namespace Nif
{
	[Serializable]
	class NiVisController : NiBoolInterpController
	{
		private readonly int dataIndex;

		public NiVisController(NiFile niFile) : base(niFile)
		{
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
	}
}