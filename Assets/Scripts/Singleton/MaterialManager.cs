using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MaterialManager : Singleton<MaterialManager>
{
	[SerializeField]
	private Shader defaultShader;

	[SerializeField]
	private Shader terrainShader;

	[SerializeField]
	private Shader skyShader;

	[SerializeField]
	private Shader waterShader;

	[SerializeField]
	private Shader nightSkyShader;

	public Shader DefaultShader => defaultShader;
	public Shader TerrainShader => terrainShader;
	public Shader NightSkyShader => nightSkyShader;
	public Shader SkyShader => skyShader;
	public Shader WaterShader => waterShader;
}