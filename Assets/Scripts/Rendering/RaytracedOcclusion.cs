public readonly struct RaytracedOcclusion : IRenderResource
{
	private readonly TextureHandle handle;

	public RaytracedOcclusion(TextureHandle handle)
	{
		this.handle = handle;
	}

	void IRenderResource.SetData(PassBuilder builder)
	{
		builder.AddKeyword("RAYTRACED_OCCLUSION");
		builder.AddResource(handle);
	}
}
