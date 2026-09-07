using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

public static class NativeArrayExtensions
{
	public static NativeArray<T> AsArray<T>(this Span<T> span) where T : unmanaged
	{
		var array = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray(span, Allocator.None);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
		var handle = AtomicSafetyHandle.Create();
		NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref array, handle);
#endif

		return array;
	}

	public static NativeArray<T> ToNativeArray<T>(this Span<T> span, Allocator allocator = Allocator.Temp) where T : unmanaged
	{
		return new(span.AsArray(), allocator);
	}
}
