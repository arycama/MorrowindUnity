using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

public static class SpanExtensions
{
	public static unsafe NativeArray<T> AsNativeArray<T>(this Span<T> span, int length) where T : unmanaged
	{
		fixed (T* buffer = span)
		{
			var nativeArray = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(buffer, length, Allocator.Temp);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
			NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref nativeArray, AtomicSafetyHandle.GetTempUnsafePtrSliceHandle());
#endif

			return nativeArray;
		}
	}

	public static NativeArray<T> AsNativeArray<T>(this Span<T> span) where T : unmanaged
	{
        var nativeArray = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray(span, Allocator.Temp);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
		NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref nativeArray, AtomicSafetyHandle.GetTempUnsafePtrSliceHandle());
#endif

		return nativeArray;
	}
}
