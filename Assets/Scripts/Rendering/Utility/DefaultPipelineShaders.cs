using System;
using UnityEngine;

[Serializable]
public class DefaultPipelineShaders
{
	[field: SerializeField] public Shader AutodeskInteractiveMaskedShader { get; private set; }
	[field: SerializeField] public Shader AutodeskInteractiveShader { get; private set; }
	[field: SerializeField] public Shader AutodeskInteractiveTransparentShader { get; private set; }
	[field: SerializeField] public Shader DefaultSpeedTree7Shader { get; private set; }
	[field: SerializeField] public Shader DefaultSpeedTree8Shader { get; private set; }
	[field: SerializeField] public Shader DefaultSpeedTree9Shader { get; private set; }
	[field: SerializeField] public Shader DefaultShader { get; private set; }
	[field: SerializeField] public Shader TerrainDetailGrassBillboardShader { get; private set; }
	[field: SerializeField] public Shader TerrainDetailGrassShader { get; private set; }
	[field: SerializeField] public Shader TerrainDetailLitShader { get; private set; }
}