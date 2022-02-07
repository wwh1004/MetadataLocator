using System;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace MetadataLocator.Test;

static class Utils {
	static readonly Dictionary<Type, uint> sizes = new();

	public static uint SizeOf(Type type) {
		if (!sizes.TryGetValue(type, out uint size)) {
			size = GetSize(type);
			sizes.Add(type, size);
		}
		return size;
	}

	static uint GetSize(Type type) {
		var dm = new DynamicMethod($"SizeOf_{type.Name}", typeof(int), Type.EmptyTypes, typeof(Utils).Module, true);
		var il = dm.GetILGenerator();
		il.Emit(OpCodes.Sizeof, type);
		il.Emit(OpCodes.Ret);
		return (uint)(int)dm.Invoke(null, null);
	}
}
