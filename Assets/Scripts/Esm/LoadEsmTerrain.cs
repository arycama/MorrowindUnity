using System;
using UnityEngine;

public class LoadEsmTerrain : MonoBehaviour
{
	[SerializeField]
	private Vector2Int coordinates;

	//[SerializeField]
	//private Color32[] colors;

	private void Start()
	{
		TerrainFactory.Create(coordinates);

		//colors = LandRecord.LandRecords[coordinate].NormalData.GetNormals().ToArray();
	}
}