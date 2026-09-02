using System;
using UnityEngine;

[Serializable]
public class DefaultPipelineMaterials
{
	[field: SerializeField] public Material DefaultMaterial { get; private set; }
	[field: SerializeField] public Material DefaultUIMaterial { get; private set; }
	[field: SerializeField] public Material Default2DMaterial { get; private set; }
	[field: SerializeField] public Material DefaultLineMaterial { get; private set; }
	[field: SerializeField] public Material DefaultParticleMaterial { get; private set; }
	[field: SerializeField] public Material DefaultTerrainMaterial { get; private set; }
	[field: SerializeField] public Material DefaultUIETC1SupportedMaterial { get; private set; }
	[field: SerializeField] public Material DefaultUIOverdrawMaterial { get; private set; }
	[field: SerializeField] public Material Default2DMaskMaterial { get; private set; }
}
