public readonly struct VolumetricLightData : IRenderResource
{
	private readonly RenderTargetHandle volumetricLight;
	private readonly BufferHandle data;

	public VolumetricLightData(RenderTargetHandle volumetricLight, BufferHandle data)
	{
		this.volumetricLight = volumetricLight;
		this.data = data;
	}

	public readonly void SetData(PassBuilder builder)
	{
		builder.AddResource(volumetricLight);
		builder.AddResource(data);
		builder.AddKeyword("VOLUMETRIC_LIGHT_ON");
	}
}
