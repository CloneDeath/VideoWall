using System;
using Silk.NET.Vulkan;

namespace VideoWall; 

public static unsafe class Helpers {
	public delegate Result ArrayAccessor<T>(ref uint length, T* dataPointer) where T : unmanaged;
	
	public static T[] GetArray<T>(ArrayAccessor<T> accessor) where T : unmanaged {
		uint length = 0;
		if (accessor(ref length, null) != Result.Success) throw new Exception("Failed to get length");

		var data = new T[length];
		fixed (T* dataPointer = data) {
			if (accessor(ref length, dataPointer) != Result.Success) throw new Exception("Failed to get data");
		}
		return data;
	}
}