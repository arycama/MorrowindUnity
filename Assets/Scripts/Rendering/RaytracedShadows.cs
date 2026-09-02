public readonly struct RaytracedShadows : IRenderResource
{
	private readonly TextureHandle handle;

	public RaytracedShadows(TextureHandle handle)
	{
		this.handle = handle;
	}

	void IRenderResource.SetData(PassBuilder builder)
	{
		builder.AddKeyword("RAYTRACED_SHADOWS");
		builder.AddResource(handle);
	}
}
