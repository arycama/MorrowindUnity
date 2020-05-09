using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Raindrop : MonoBehaviour
{
	private void Start()
	{
		var reader = BsaFileReader.LoadArchiveFileData($"meshes\\rainsplash.nif");
		var file = new Nif.NiFile(reader);

		var go = file.CreateGameObject();

		var mesh = go.GetComponentInChildren<MeshFilter>().sharedMesh;
		var material = go.GetComponentInChildren<Renderer>().sharedMaterial;

		var particleSystemRenderer = GetComponent<ParticleSystemRenderer>();
		particleSystemRenderer.mesh = mesh;
		particleSystemRenderer.material = material;

		Destroy(go);
	}
}