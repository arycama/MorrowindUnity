public readonly struct VolumetricLightData : IRenderResource
{
	private readonly TextureHandle volumetricLight;
	private readonly BufferHandle data;

	public VolumetricLightData(TextureHandle volumetricLight, BufferHandle data)
	{
		this.volumetricLight = volumetricLight;
		this.data = data;
	}

	public readonly void SetData(PassBuilder builder)
	{
		if (builder.RenderGraph.IsResourceWritten(volumetricLight))
		{
			builder.AddResource(volumetricLight);
			builder.AddResource(data);
			builder.AddKeyword("VOLUMETRIC_LIGHT_ON");
		}
	}
}
