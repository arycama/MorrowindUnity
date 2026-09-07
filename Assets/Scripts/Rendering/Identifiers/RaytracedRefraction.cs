public readonly struct RaytracedRefraction : IRenderResource
{
	private readonly RenderTargetHandle handle;

	public RaytracedRefraction(RenderTargetHandle handle)
	{
		this.handle = handle;
	}

	void IRenderResource.SetData(PassBuilder builder)
	{
		builder.AddKeyword("RAYTRACED_REFRACTION");
		builder.AddResource(handle);
	}
}
