using System;
using UnityEngine;
using System.IO;

namespace Nif
{
	[Serializable]
	class NiCamera : NiAVObject
	{
		[SerializeField]
		private float left, right, top, bottom, near, far;

		[SerializeField]
		private bool useOrthographicProjection;

		[SerializeField]
		private float viewportLeft, viewportRight, viewportTop, viewportBottom, lodAdjust;

		[SerializeField]
		private int unknownLink, unknownInt, unknownInt2;

		public NiCamera(NiFile niFile) : base(niFile)
		{
			left = niFile.Reader.ReadSingle();
			right = niFile.Reader.ReadSingle();
			top = niFile.Reader.ReadSingle();
			bottom = niFile.Reader.ReadSingle();
			near = niFile.Reader.ReadSingle();
			far = niFile.Reader.ReadSingle();

			useOrthographicProjection = niFile.Reader.ReadInt32() != 0;

			viewportLeft = niFile.Reader.ReadSingle();
			viewportRight = niFile.Reader.ReadSingle();
			viewportTop = niFile.Reader.ReadSingle();
			viewportBottom = niFile.Reader.ReadSingle();
			lodAdjust = niFile.Reader.ReadSingle();

			unknownLink = niFile.Reader.ReadInt32();
		}

		public override void ProcessNiObject(NiObjectNet niObject)
		{
			var transform = Camera.main.transform;
			transform.SetParent(niObject.GameObject.transform, false);
			transform.localPosition = position;
			transform.localEulerAngles = new Vector3(90, 0, 0);
		}
	}
}