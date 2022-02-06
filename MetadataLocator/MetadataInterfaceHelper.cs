using System;
using System.Reflection;
using System.Runtime.InteropServices;
using static MetadataLocator.NativeMethods;

namespace MetadataLocator;

/// <summary>
/// Metadata interface helper
/// </summary>
public static class MetadataInterfaceHelper {
	static readonly bool isClr2x;
	static readonly MethodInfo getMetadataImport;

	static MetadataInterfaceHelper() {
		isClr2x = Environment.Version.Major == 2;
		getMetadataImport = typeof(ModuleHandle).GetMethod("_GetMetadataImport", isClr2x ? BindingFlags.NonPublic | BindingFlags.Instance : BindingFlags.NonPublic | BindingFlags.Static);
	}

	/// <summary>
	/// A wrapper for _GetMetadataImport
	/// </summary>
	/// <param name="module"></param>
	/// <returns></returns>
	public static unsafe nuint GetMetadataImport(Module module) {
		if (module is null)
			throw new ArgumentNullException(nameof(module));
			
		return isClr2x
			? (nuint)Pointer.Unbox(getMetadataImport.Invoke(module.ModuleHandle, null))
			: (nuint)(nint)getMetadataImport.Invoke(null, new object[] { module });
	}

	/// <summary>
	/// Get the instance of <see cref="IMetaDataTables"/> from IMDInternalImport
	/// </summary>
	/// <param name="pIMDInternalImport">A pointer to the instance of IMDInternalImport</param>
	/// <returns></returns>
	public static IMetaDataTables GetIMetaDataTables(nuint pIMDInternalImport) {
		if (pIMDInternalImport == 0)
			throw new ArgumentNullException(nameof(pIMDInternalImport));

		int result = GetMetaDataPublicInterfaceFromInternal(pIMDInternalImport, IID_IMetaDataTables, out nuint pIMetaDataTables);
		return result == 0 ? GetManagedInterface<IMetaDataTables>(pIMetaDataTables) : null;
	}

	static int GetMetaDataPublicInterfaceFromInternal(nuint pv, Guid riid, out nuint ppv) {
		return isClr2x ? GetMetaDataPublicInterfaceFromInternal2(pv, riid, out ppv) : GetMetaDataPublicInterfaceFromInternal4(pv, riid, out ppv);
	}

	static T GetManagedInterface<T>(nuint pIUnknown) where T : class {
		if (pIUnknown == 0)
			throw new ArgumentNullException(nameof(pIUnknown));

		return (T)Marshal.GetObjectForIUnknown((nint)pIUnknown);
	}
}
