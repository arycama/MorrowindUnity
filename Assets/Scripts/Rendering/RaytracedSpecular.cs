public readonly struct RaytracedSpecular : IRenderResource
{
	private readonly RenderTargetHandle handle;

	public RaytracedSpecular(RenderTargetHandle handle)
	{
		this.handle = handle;
	}

	void IRenderResource.SetData(PassBuilder builder)
	{
		builder.AddKeyword("RAYTRACED_SPECULAR");
		builder.AddResource(handle);
	}
}
