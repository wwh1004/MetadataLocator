using System;
using System.Text;
using static NativeSharp.NativeMethods;

namespace NativeSharp {
	unsafe partial class NativeProcess {
		internal static void* GetModuleHandleInternal(void* processHandle, bool first, string moduleName) {
			void* moduleHandle;
			uint size;
			void*[] moduleHandles;
			StringBuilder moduleNameBuffer;

			if (!EnumProcessModulesEx(processHandle, &moduleHandle, (uint)IntPtr.Size, out size, LIST_MODULES_ALL))
				return null;
			if (first)
				return moduleHandle;
			moduleHandles = new void*[size / (uint)IntPtr.Size];
			fixed (void** p = moduleHandles)
				if (!EnumProcessModulesEx(processHandle, p, size, out _, LIST_MODULES_ALL))
					return null;
			moduleNameBuffer = new StringBuilder((int)MAX_MODULE_NAME32);
			for (int i = 0; i < moduleHandles.Length; i++) {
				if (!GetModuleBaseName(processHandle, moduleHandles[i], moduleNameBuffer, MAX_MODULE_NAME32))
					return null;
				if (moduleNameBuffer.ToString().Equals(moduleName, StringComparison.OrdinalIgnoreCase))
					return moduleHandles[i];
			}
			return null;
		}
	}
}
