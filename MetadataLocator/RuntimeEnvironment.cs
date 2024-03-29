using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace MetadataLocator;

enum RuntimeFlavor {
	/// <summary>
	/// .NET Framework 1.0 ~ 4.8
	/// </summary>
	Framework,

	/// <summary>
	/// .NET Core 1.0 ~ 3.1 and .NET 5.0 +
	/// </summary>
	Core
}

enum RuntimeVersion {
	Fx20,
	Fx40,
	Fx45,
	Fx453,
	Core10,
	Core30,
	Core31,
	Core50,
	Core60,
}

static class RuntimeEnvironment {
	static readonly Version fx45 = new(4, 0, 30319, 17000);
	static readonly Version fx453 = new(4, 5, 0, 0); // .NET 4.6 Preview

	public static RuntimeFlavor Flavor { get; } = GetRuntimeFlavor();

	public static RuntimeVersion Version { get; } = GetRuntimeVersion();

	static RuntimeFlavor GetRuntimeFlavor() {
		switch (typeof(object).Module.ScopeName) {
		case "CommonLanguageRuntimeLibrary": return RuntimeFlavor.Framework;
		case "System.Private.CoreLib.dll": return RuntimeFlavor.Core;
		default: throw new NotSupportedException();
		}
	}

	static RuntimeVersion GetRuntimeVersion() {
		var version = Environment.Version;
		int major = version.Major;
		switch (Flavor) {
		case RuntimeFlavor.Framework: {
			if (major == 4) {
				var path = new StringBuilder(MAX_PATH);
				if (!GetModuleFileName(GetModuleHandle("clr.dll"), path, MAX_PATH))
					break;
				var fileVersion = GetFileVersion(path.ToString());
				if (fileVersion >= fx453)
					return RuntimeVersion.Fx453;
				if (fileVersion >= fx45)
					return RuntimeVersion.Fx45;
				return RuntimeVersion.Fx40;
			}
			else if (major == 2) {
				return RuntimeVersion.Fx20;
			}
			break;
		}
		case RuntimeFlavor.Core: {
			if (major == 4)
				return RuntimeVersion.Core10;
			// Improve .NET Core version APIs: https://github.com/dotnet/runtime/issues/28701
			// Environment.Version works fine since .NET Core v3.0
			int minor = version.Minor;
			Debug2.Assert(major <= 6, "Update RuntimeDefinitions if need");
			if (major >= 6)
				return RuntimeVersion.Core60;
			if (major >= 5)
				return RuntimeVersion.Core50;
			if (major >= 3) {
				if (minor >= 1)
					return RuntimeVersion.Core31;
				return RuntimeVersion.Core30;
			}
			break;
		}
		}
		Debug2.Assert(false);
		throw new NotSupportedException();
	}

	static Version GetFileVersion(string filePath) {
		var versionInfo = FileVersionInfo.GetVersionInfo(filePath);
		return new Version(versionInfo.FileMajorPart, versionInfo.FileMinorPart, versionInfo.FileBuildPart, versionInfo.FilePrivatePart);
	}

	#region NativeMethods
	const ushort MAX_PATH = 260;

	[DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, SetLastError = true)]
	static extern nuint GetModuleHandle(string? lpModuleName);

	[DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	static extern bool GetModuleFileName(nuint hModule, StringBuilder lpFilename, uint nSize);
	#endregion
}
