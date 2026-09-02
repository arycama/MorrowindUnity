using System;
using UnityEngine;

public class LightCulling
{
	[Serializable]
	public class Settings
	{
		[field: SerializeField, Pow2(128)] public int TileSize { get; private set; } = 16;
		[field: SerializeField, Pow2(8192)] public int DepthSlices { get; private set; } = 8192;
		[field: SerializeField] public Mesh PointLightMesh { get; private set; }
		[field: SerializeField] public Mesh SpotLightMesh { get; private set; }
	}
}