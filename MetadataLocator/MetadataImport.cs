using System;
using System.Diagnostics;
using SR = System.Reflection;

namespace MetadataLocator;

/// <summary>
/// IMDInternalImport function
/// </summary>
public enum MetadataImportFunction {
	/// <summary>
	/// IMDInternalImport.GetVersionString
	/// </summary>
	GetVersionString
}

/// <summary>
/// IMDInternalImport info
/// </summary>
public sealed unsafe class MetadataImport {
	sealed class Profile {
		public readonly Guid IID;
		public readonly uint GetVersionString;

		public Profile(Guid iid, uint getVersionString) {
			IID = iid;
			GetVersionString = getVersionString;
		}
	}

	static readonly SR.MethodInfo getMetadataImport = typeof(ModuleHandle).GetMethod("_GetMetadataImport", SR.BindingFlags.NonPublic | SR.BindingFlags.Instance | SR.BindingFlags.Static);
	static readonly Profile[] profiles = {
		new(new(0xce0f34ed, 0xbbc6, 0x11d2, 0x94, 0x1e, 0x00, 0x00, 0xf8, 0x08, 0x34, 0x60), 102), // fx20 ~ core31
		new(new(0x1b119f60, 0xc507, 0x4024, 0xbb, 0x39, 0xf8, 0x22, 0x3f, 0xb3, 0xe1, 0xfd), 92) // core50+
	};
	static Profile? cachedProfile;

	// IMDInternalImport interface contains breaking change in this commit: https://github.com/dotnet/coreclr/commit/ef7767a3ba1c0a34b55cbd5496b799b17218ca14#diff-1a774ef53fa72817aedde61a45204358e0cd0a76272780558180dc27a2f85cb5L309
	// so we must call QueryInterface to judge which version IMDInternalImport we are using

	/// <summary>
	/// Empty instance
	/// </summary>
	public static readonly MetadataImport Empty = new(0);

	/// <summary>
	/// Determine if current instance is invalid
	/// </summary>
	public bool IsInvalid => This == 0;

	/// <summary>
	/// Instance of IMDInternalImport
	/// </summary>
	public nuint This { get; }

	/// <summary>
	/// Virtual function vtable
	/// </summary>
	public nuint Vfptr => *(nuint*)This;

	MetadataImport(nuint @this) {
		This = @this;
	}

	/// <summary>
	/// A wrapper for _GetMetadataImport to get instance of IMDInternalImport interface
	/// </summary>
	/// <param name="module"></param>
	/// <returns></returns>
	public static MetadataImport Create(SR.Module module) {
		if (module is null)
			throw new ArgumentNullException(nameof(module));

		nuint pMDImport = getMetadataImport.IsStatic
			? (nuint)(nint)getMetadataImport.Invoke(null, new object[] { module })
			: (nuint)SR.Pointer.Unbox(getMetadataImport.Invoke(module.ModuleHandle, null));
		return pMDImport != 0 ? new MetadataImport(pMDImport) : Empty;
	}

	/// <summary>
	/// Get virtual function address
	/// </summary>
	/// <param name="function"></param>
	/// <returns></returns>
	/// <exception cref="NotSupportedException"></exception>
	public nuint GetFunction(MetadataImportFunction function) {
		var profile = GetProfile(This);
		switch (function) {
		case MetadataImportFunction.GetVersionString:
			return ((nuint*)Vfptr)[profile.GetVersionString];
		default:
			throw new NotSupportedException();
		}
	}

	/// <summary>
	/// Call IMDInternalImport.GetVersionString and return pVersion
	/// </summary>
	/// <returns></returns>
	public nuint GetVersionString() {
		var getVersionString = (delegate* unmanaged[Stdcall]<nuint, out nuint, int>)GetFunction(MetadataImportFunction.GetVersionString);
		int hr = getVersionString(This, out nuint pVersion);
		Debug2.Assert(hr >= 0);
		return pVersion;
	}

	static Profile GetProfile(nuint pMDImport) {
		Debug2.Assert(pMDImport != 0);
		if (cachedProfile is not null)
			return cachedProfile;

		var vfptr = *(nuint**)pMDImport;
		var queryInterface = (delegate* unmanaged[Stdcall]<nuint, in Guid, out nuint, int>)vfptr[0];
		var release = (delegate* unmanaged[Stdcall]<nuint, uint>)vfptr[2];
		foreach (var profile in profiles) {
			int hr = queryInterface(pMDImport, profile.IID, out nuint pUnk);
			if (hr < 0)
				continue;

			Debug2.Assert(pUnk == pMDImport);
			release(pMDImport);
			cachedProfile = profile;
			return profile;
		}

		throw new NotSupportedException("Please update IMDInternalImport profiles");
	}
}
