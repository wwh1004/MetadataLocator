using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace MetadataLocator;

static class ReflectionHelpers {
	static readonly FieldInfo moduleHandleField = GetModuleHandleField();

	static FieldInfo GetModuleHandleField() {
		if (RuntimeEnvironment.Version < RuntimeVersion.Fx40)
			return typeof(ModuleHandle).GetField("m_ptr", BindingFlags.NonPublic | BindingFlags.Instance);
		return typeof(object).Module.GetType("System.Reflection.RuntimeModule").GetField("m_pData", BindingFlags.NonPublic | BindingFlags.Instance);
	}

	public static nuint GetModuleHandle(Module module) {
		if (RuntimeEnvironment.Version < RuntimeVersion.Fx40)
			return (nuint)(nint)moduleHandleField.GetValue(module.ModuleHandle);
		return (nuint)(nint)moduleHandleField.GetValue(module);
	}

	public static nuint GetNativeModuleHandle(Module module) {
		if (module is null)
			throw new ArgumentNullException(nameof(module));

		return (nuint)(nint)Marshal.GetHINSTANCE(module);
	}
}
