public readonly struct SceneRtas : IRenderResource
{
	public readonly RayTracingAccelerationStructureHandle handle;

	public SceneRtas(RayTracingAccelerationStructureHandle handle)
	{
		this.handle = handle;
	}

	public void SetData(PassBuilder builder)
	{
		builder.AddResource(handle);
	}
}
