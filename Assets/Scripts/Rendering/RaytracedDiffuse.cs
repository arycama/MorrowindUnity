public readonly struct RaytracedDiffuse : IRenderResource
{
	private readonly TextureHandle handle;

	public RaytracedDiffuse(TextureHandle handle)
	{
		this.handle = handle;
	}

	void IRenderResource.SetData(PassBuilder builder)
	{
		builder.AddKeyword("RAYTRACED_DIFFUSE");
		builder.AddResource(handle);
	}
}
