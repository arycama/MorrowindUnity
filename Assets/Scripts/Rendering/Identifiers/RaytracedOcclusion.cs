public readonly struct RaytracedOcclusion : IRenderResource
{
	private readonly RenderTargetHandle handle;

	public RaytracedOcclusion(RenderTargetHandle handle)
	{
		this.handle = handle;
	}

	void IRenderResource.SetData(PassBuilder builder)
	{
		builder.AddKeyword("RAYTRACED_OCCLUSION");
		builder.AddResource(handle);
	}
}
