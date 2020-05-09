using System;
using System.Collections.Generic;
using Nif;
using UnityEngine;
using Esm;

public abstract class CreatableRecord : EsmRecord
{
	protected static Dictionary<string, NiFile> niCache = new Dictionary<string, NiFile>();

	[SerializeField]
	protected string fullName;

	[SerializeField]
	protected string model;

	[SerializeField]
	protected Script script;

	public string FullName => fullName;

	public virtual GameObject CreateGameObject(ReferenceData referenceData, Transform parent = null)
	{
		// Used for lights which don't have a model
		if (string.IsNullOrEmpty(model))
		{
			return new GameObject(name);
		}

		NiFile niFile;
		if (!niCache.TryGetValue(model, out niFile))
		{
			var reader = BsaFileReader.LoadArchiveFileData($"meshes\\{model}");
			niFile = new NiFile(reader);
			niCache.Add(model, niFile);
		}

		var gameObject = niFile.CreateGameObject(parent);

		// Why is this not being used? Problems with loading animation skeletons maybe?
		//gameObject.name = record.Id;

		if (gameObject.name.ToLower().Contains("marker"))
		{
			var transforms = gameObject.GetComponentsInChildren<Transform>();
			foreach (var transform in transforms)
			{
				transform.gameObject.tag = "Marker";
				transform.gameObject.layer = LayerMask.NameToLayer("Hidden");
			}

			// Return because markers don't need collision meshes
			return gameObject;
		}

		// If there is no rootcollisionnode, add a collider to evey visible mesh
		var rootCollision = gameObject.transform.Find("RootCollisionNode");
		if (rootCollision == null)
		{
			var meshFilters = gameObject.GetComponentsInChildren<MeshFilter>();
			foreach (var meshFilter in meshFilters)
			{
				switch (meshFilter.gameObject.tag)
				{
					case "Hidden":
					case "Marker":
						meshFilter.gameObject.layer = LayerMask.NameToLayer("Hidden");
						continue;
					case "No Collider":
						meshFilter.gameObject.layer = LayerMask.NameToLayer("Raycast Only");
						break;
				}

				var meshCollider = meshFilter.gameObject.AddComponent<MeshCollider>();
				meshCollider.sharedMesh = meshFilter.sharedMesh;
			}
		}
		else
		{
			// If a root collisionnode exists, set all it's children to ignore raycast, so they do not block crosshair targets, etc.
			var meshColliders = rootCollision.GetComponentsInChildren<MeshCollider>();
			foreach (var meshCollider in meshColliders)
			{
				meshCollider.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
			}

			// Also add a visible mesh collider for each mesh, but set to raycast only. This way, the rootcollisions will still only be used for collisions
			var meshFilters = gameObject.GetComponentsInChildren<MeshFilter>();
			var meshFiltersLength = meshFilters.Length;
			for(var i = 0; i < meshFiltersLength; i++)
			{
				var meshFilter = meshFilters[i];
				switch (meshFilter.gameObject.tag)
				{
					case "Hidden":
					case "Marker":
						meshFilter.gameObject.layer = LayerMask.NameToLayer("Hidden");
						continue;
				}

				// Add a collider to each visible mesh. We only want this to affect raycasts
				meshFilter.gameObject.layer = LayerMask.NameToLayer("Raycast Only");
				var meshCollider = meshFilter.gameObject.AddComponent<MeshCollider>();
				meshCollider.sharedMesh = meshFilter.sharedMesh;
			}
		}

		return gameObject;
	}
}