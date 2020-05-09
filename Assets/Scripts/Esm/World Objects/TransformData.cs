using System;
using UnityEngine;

namespace Esm
{
	[Serializable]
	public class TransformData
	{
		[SerializeField]
		private Vector3 position;

		[SerializeField]
		private Vector3 rotation;

		public TransformData(System.IO.BinaryReader reader)
		{
			position = reader.ReadVector3();
			rotation = reader.ReadEulerAngle();
		}

		public void SetTransformData(Transform transform)
		{
			transform.position += position;
			transform.rotation = Quaternion.AngleAxis(rotation.x, Vector3.right) * Quaternion.AngleAxis(rotation.z, Vector3.forward) * Quaternion.AngleAxis(rotation.y, Vector3.up);
		}
	}
}