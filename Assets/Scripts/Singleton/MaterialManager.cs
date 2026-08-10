using UnityEngine;

public class MaterialManager : Singleton<MaterialManager>
{
	[field: SerializeField]	public Shader DefaultShader{ get; private set; }
	[field: SerializeField]	public Shader TerrainShader{ get; private set; }
	[field: SerializeField]	public Shader WaterShader { get; private set; }
	[field: SerializeField]	public Shader AtmosphereShader { get; private set; }
	[field: SerializeField]	public Shader SkyShader { get; private set; }
	[field: SerializeField]	public Shader NightSkyShader { get; private set; }
}