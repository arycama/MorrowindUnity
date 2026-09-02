using System;
using System.Collections.Generic;

public class ResourceMap
{
	private readonly Dictionary<Type, IRenderResource> resources = new();

	public void SetResource<T>(T resource) where T : IRenderResource
	{
		resources[typeof(T)] = resource;
	}

	public T GetResource<T>()
	{
		return (T)resources[typeof(T)];
	}

	public void Clear()
	{
		resources.Clear();
	}
}
