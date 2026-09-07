public readonly struct RaytracedDiffuse : IRenderResource
{
	private readonly RenderTargetHandle handle;

	public RaytracedDiffuse(RenderTargetHandle handle)
	{
		this.handle = handle;
	}

	void IRenderResource.SetData(PassBuilder builder)
	{
		builder.AddKeyword("RAYTRACED_DIFFUSE");
		builder.AddResource(handle);
	}
}
