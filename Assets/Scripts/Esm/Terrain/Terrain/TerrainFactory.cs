using System;
using System.Collections.Generic;
using Esm;
using UnityEngine;

public class TerrainFactory
{
	[Flags]
	private enum Directions
	{
		None = 0x0,
		SW = 0x1,
		S = 0x2,
		SE = 0x4,
		W = 0x8,
		E = 0x20,
		NW = 0x40,
		N = 0x80,
		NE = 0x100
	};

	public static void Create(Vector2Int coordinates)
	{
		var record = LandRecord.Get(coordinates);
		var gameObject = new GameObject(coordinates.ToString());
		gameObject.transform.position = new Vector3(coordinates.x * 8192, 0, coordinates.y * 8192);

		var meshFilter = gameObject.AddComponent<MeshFilter>();

		var meshRenderer = gameObject.AddComponent<MeshRenderer>();
		meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
		meshRenderer.sharedMaterial = new Material(MaterialManager.Instance.TerrainShader) { enableInstancing = true };
		meshRenderer.sharedMaterial.mainTextureScale = new Vector2(16, 16);

		// Generate the mesh
		int vertexStep = 8192 / 64;

		// Generate vertices and appropriate heights
		var vertices = new Vector3[65 * 65];
		var uvs = new Vector4[vertices.Length];
		var nextColHeight = record.HeightData.ReferenceHeight;

		var triangles = new int[64 * 64 * 6];

		for (int y = 0, i = 0; y < 65; y++)
		{
			var previousHeight = nextColHeight;
			for (var x = 0; x < 65; x++, i++) 
			{
				var height = previousHeight + record.HeightData.HeightPoints[i]; // this is the change in elevation from the previous (to the leftt) vertex, multiplied by 8

				if (x == 0)
				{
					nextColHeight = height;
				}

				var vertex = new Vector3(x * vertexStep, height * 8, y * vertexStep); // Each vertex is 128 game-units apart
				vertices[i] = vertex;
				previousHeight = height;

				// Generate UV too ( every 4 patches should be one UV)
				var uvX = Mathf.Lerp(1f / 18f, 1 - 1f / 18f, x / 64f);
				var uvY = Mathf.Lerp(1f / 18f, 1 - 1f / 18f, y / 64f);
				var uvZ = x / 64f;
				var uvW = y / 64f;

                uvs[x + y * 65] = new Vector4(uvX, uvY, uvZ, uvW);
			}
		}

		// Triangles
		// Needs to be seperate from previous loop
		for (int ti = 0, vi = 0, y = 0; y < 64; y++, vi++)
		{
			for (int x = 0; x < 64; x++, ti += 6, vi++)
			{
				triangles[ti] = vi;
				triangles[ti + 3] = triangles[ti + 2] = vi + 1;
				triangles[ti + 4] = triangles[ti + 1] = vi + 64 + 1;
				triangles[ti + 5] = vi + 64 + 2;
			}
		}

		var normals = new Vector3[record.NormalData.Normals.Length / 3];
		for (var i = 0; i < normals.Length; i++)
		{
			normals[i] = new Vector3(record.NormalData.Normals[i * 3] / 128f, record.NormalData.Normals[i * 3 + 2] / 128f, record.NormalData.Normals[i * 3 + 1] / 128f);
		}

		var mesh = new Mesh
		{
			vertices = vertices,
			triangles = triangles,
			normals = normals
		};

		mesh.SetUVs(0, uvs);

		if(record.ColorData != null)
			mesh.colors32 = record.ColorData.Colors;

		meshFilter.sharedMesh = mesh;

		// Now calculate vertex colors. Look at each "patch". if the neighbouring patch is a different texture, then set the alpha between the pixels to 0 or something

		// Remaining steps are for textures only
		if (record.TextureData == null)
		{
			return;
		}

		// Get the texturedata, and surrounding cells
		Directions cellDirections = 0;
		var borderCells = new TextureData[3, 3];
		for (var y = 0; y < 3; y++)
		{
			for (var x = 0; x < 3; x++)
			{
				var xCoord = coordinates.x + (x - 1);
				var yCoord = coordinates.y + (y - 1);
				var coordinate = new Vector2Int(xCoord, yCoord);
				LandRecord landRecord;
				if (LandRecord.Records.TryGetValue(coordinate, out landRecord))
				{
					borderCells[x, y] = landRecord.TextureData;
					cellDirections |= (Directions)(Mathf.Pow(2, x + y * 3));
				}
			}
		}

		// Don't do this inside the loop, silly
		var currentIndices = record.TextureData.TextureIndices;
		var borderIndices = GetBorderIndices(cellDirections, borderCells); // Get an 18x18 array, which includes the surrounding textures

		var control = new Texture2D(18, 18, TextureFormat.R8, false, true)
		{
			filterMode = FilterMode.Point
		};

		for(var y = 0; y < control.height; y++)
		{
			for(var x = 0; x < control.width; x++)
			{
				var textureIndex = borderIndices[x, y];
				var color = new Color32((byte)textureIndex, 0, 0, 0);
				control.SetPixel(x, y, color);
			}
		}

		control.Apply(false, true);

		meshRenderer.sharedMaterial.SetTexture("_MainTex", LandTextureRecord.GetTexture2DArray());
		meshRenderer.sharedMaterial.SetTexture("_Control", control);

		gameObject.AddComponent<MeshCollider>();
	}

	private static int[,] GetBorderIndices(Directions cellDirections, TextureData[,] borderCells)
	{
		// Get the first row/column
		var borderIndices = new int[18, 18];
		var currentIndices = borderCells[1, 1].TextureIndices;

		// Copy the existing indices into a new array, starting at 1,1
		for (var y = 0; y < 16; y++)
		{
			for (var x = 0; x < 16; x++)
			{
				borderIndices[x + 1, y + 1] = currentIndices[x, y];
			}
		}

		// Now do each of the directions
		// Try and figure out a way to reduce this code
		// Southwest (Zero)
		if (cellDirections.HasFlag(Directions.SW))
		{
			borderIndices[0, 0] = borderCells[0, 0].TextureIndices[15, 15];
		}
		else
		{
			// Technically, this should check the West and South cells too, and see if one of those has a texture, but eh. 
			borderIndices[0, 0] = borderIndices[1, 1];
		}

		// South (One)
		if (cellDirections.HasFlag(Directions.S))
		{
			for (var i = 0; i < 16; i++)
			{
				borderIndices[i + 1, 0] = borderCells[1, 0].TextureIndices[i, 15];
			}
		}
		else
		{
			// If no south cell, duplicate the bottom layer
			for (var i = 0; i < 16; i++)
			{
				borderIndices[i + 1, 0] = borderIndices[i + 1, 1];
			}
		}

		// SouthEast (Two)
		if (cellDirections.HasFlag(Directions.SE))
		{
			borderIndices[17, 0] = borderCells[2, 0].TextureIndices[0, 15];
		}
		else
		{
			borderIndices[17, 0] = borderIndices[16, 1];
		}

		// West (Three)
		if (cellDirections.HasFlag(Directions.W))
		{
			for (var i = 0; i < 16; i++)
			{
				borderIndices[0, i + 1] = borderCells[0, 1].TextureIndices[15, i];
			}
		}
		else
		{
			// If no west cell, duplicate the leftmost column
			for (var i = 0; i < 16; i++)
			{
				borderIndices[0, i + 1] = borderIndices[1, i + 1];
			}
		}

		// East (Four (or five?)
		if (cellDirections.HasFlag(Directions.E))
		{
			for (var i = 0; i < 16; i++)
			{
				borderIndices[17, i + 1] = borderCells[2, 1].TextureIndices[0, i];
			}
		}
		else
		{
			// If no east cell, duplicate the rightmost column
			for (var i = 0; i < 16; i++)
			{
				borderIndices[17, i + 1] = borderIndices[16, i + 1];
			}
		}

		// Northwest (Six?)
		if (cellDirections.HasFlag(Directions.NW))
		{
			borderIndices[0, 17] = borderCells[0, 2].TextureIndices[15, 0];
		}
		else
		{
			// Technically, this should check the West and South cells too, and see if one of those has a texture, but eh. 
			borderIndices[0, 17] = borderIndices[1, 16];
		}

		// North (Seven?)
		if (cellDirections.HasFlag(Directions.N))
		{
			for (var i = 0; i < 16; i++)
			{
				borderIndices[i + 1, 17] = borderCells[1, 2].TextureIndices[i, 0];
			}
		}
		else
		{
			// If no North cell, duplicate the bottom layer
			for (var i = 0; i < 16; i++)
			{

				borderIndices[i + 1, 17] = borderIndices[i + 1, 16];
			}
		}

		// NorthEast (Eight)
		if (cellDirections.HasFlag(Directions.NE))
		{
			borderIndices[17, 17] = borderCells[2, 2].TextureIndices[0, 0];
		}
		else
		{
			borderIndices[17, 17] = borderIndices[16, 16];
		}

		return borderIndices;
	}
}