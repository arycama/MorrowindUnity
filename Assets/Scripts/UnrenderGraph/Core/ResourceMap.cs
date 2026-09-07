using System;
using System.Collections.Generic;

public class ResourceMap
{
	private readonly Dictionary<Type, IRenderResource> resources = new();

	public void SetResource<T>(T resource) where T : IRenderResource
	{
		resources[typeof(T)] = resource;
	}

	public bool TryGetResource(Type type, out IRenderResource resource)
	{
		return resources.TryGetValue(type, out resource);
	}

	public bool TryGetResource<T>(out T resource)
	{
		var resourceExists = TryGetResource(typeof(T), out var temp);
		resource = (T)temp;
		return resourceExists;
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
