public readonly struct RaytracedDepthOfField : IRenderResource
{
	private readonly RenderTargetHandle handle;

	public RaytracedDepthOfField(RenderTargetHandle handle)
	{
		this.handle = handle;
	}

	void IRenderResource.SetData(PassBuilder builder)
	{
		builder.AddKeyword("RAYTRACED_DEPTH_OF_FIELD");
		builder.AddResource(handle);
	}
}
