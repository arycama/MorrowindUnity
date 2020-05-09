using System;
using UnityEngine;

[Serializable]
public class CellData
{
	[SerializeField, EnumFlags]
	private CellFlags flags;

	[SerializeField]
	private Vector2Int coordinates;

	public CellData(System.IO.BinaryReader reader)
	{
		flags = (CellFlags)reader.ReadInt32();

		var x = reader.ReadInt32();
		var y = reader.ReadInt32();
		coordinates = new Vector2Int(x, y);
	}

	public bool HasWater => flags.HasFlag(CellFlags.HasWater);

	public bool IsInterior => flags.HasFlag(CellFlags.Interior);

	public Vector2Int Coordinates => coordinates;

	public void LoadTerrain()
	{
		// Load the terrain
		if (!IsInterior)
		{
			TerrainFactory.Create(coordinates);
		}
	}
}