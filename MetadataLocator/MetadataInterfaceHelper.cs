using System;
using System.Reflection;
using System.Runtime.InteropServices;
using static MetadataLocator.NativeMethods;

namespace MetadataLocator {
	/// <summary>
	/// Metadata interface helper
	/// </summary>
	public static unsafe class MetadataInterfaceHelper {
		private static readonly bool _isClr2x;
		private static readonly MethodInfo _getMetadataImport;

		static MetadataInterfaceHelper() {
			_isClr2x = Environment.Version.Major == 2;
			_getMetadataImport = typeof(ModuleHandle).GetMethod("_GetMetadataImport", _isClr2x ? BindingFlags.NonPublic | BindingFlags.Instance : BindingFlags.NonPublic | BindingFlags.Static);
		}

		/// <summary>
		/// A wrapper for _GetMetadataImport
		/// </summary>
		/// <param name="module"></param>
		/// <returns></returns>
		public static void* GetMetadataImport(Module module) {
			if (module is null)
				throw new ArgumentNullException(nameof(module));

			return _isClr2x
				? Pointer.Unbox(_getMetadataImport.Invoke(module.ModuleHandle, null))
				: (void*)(IntPtr)_getMetadataImport.Invoke(null, new object[] { module });
		}

		/// <summary>
		/// Get the instance of <see cref="IMetaDataTables"/> from IMDInternalImport
		/// </summary>
		/// <param name="pIMDInternalImport">A pointer to the instance of IMDInternalImport</param>
		/// <returns></returns>
		public static IMetaDataTables GetIMetaDataTables(void* pIMDInternalImport) {
			if (pIMDInternalImport == null)
				throw new ArgumentNullException(nameof(pIMDInternalImport));

			int result;
			void* pIMetaDataTables;
			fixed (Guid* riid = &IID_IMetaDataTables)
				result = GetMetaDataPublicInterfaceFromInternal(pIMDInternalImport, riid, &pIMetaDataTables);
			return result == 0 ? GetManagedInterface<IMetaDataTables>(pIMetaDataTables) : null;
		}

		private static int GetMetaDataPublicInterfaceFromInternal(void* pv, Guid* riid, void** ppv) {
			return _isClr2x ? GetMetaDataPublicInterfaceFromInternal2(pv, riid, ppv) : GetMetaDataPublicInterfaceFromInternal4(pv, riid, ppv);
		}

		private static T GetManagedInterface<T>(void* pIUnknown) where T : class {
			if (pIUnknown == null)
				throw new ArgumentNullException(nameof(pIUnknown));

			return (T)Marshal.GetObjectForIUnknown((IntPtr)pIUnknown);
		}
	}
}
