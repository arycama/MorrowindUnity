public readonly struct RaytracedShadows : IRenderResource
{
	private readonly RenderTargetHandle handle;

	public RaytracedShadows(RenderTargetHandle handle)
	{
		this.handle = handle;
	}

	void IRenderResource.SetData(PassBuilder builder)
	{
		builder.AddKeyword("RAYTRACED_SHADOWS");
		builder.AddResource(handle);
	}
}
