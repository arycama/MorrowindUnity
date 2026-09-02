public readonly struct RaytracedDepthOfField : IRenderResource
{
	private readonly TextureHandle handle;

	public RaytracedDepthOfField(TextureHandle handle)
	{
		this.handle = handle;
	}

	void IRenderResource.SetData(PassBuilder builder)
	{
		builder.AddKeyword("RAYTRACED_DEPTH_OF_FIELD");
		builder.AddResource(handle);
	}
}
