using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Rain : MonoBehaviour
{
	[SerializeField]
	private Transform target;

	private void Start()
	{
		var reader = BsaFileReader.LoadArchiveFileData($"meshes\\raindrop.nif");
		var nif = new Nif.NiFile(reader);
		var go = nif.CreateGameObject();

		var mesh = go.GetComponentInChildren<MeshFilter>().sharedMesh;
		var material = go.GetComponentInChildren<Renderer>().sharedMaterial;

		var particleSystemRenderer = GetComponent<ParticleSystemRenderer>();
		particleSystemRenderer.mesh = mesh;
		particleSystemRenderer.material = material;

		Destroy(go);
	}

	private void Update()
	{
		transform.position = target.position;
		transform.eulerAngles = new Vector3(0, target.eulerAngles.y, 0);
	}
}