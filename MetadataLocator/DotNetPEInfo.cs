using System;
using System.Reflection;

namespace MetadataLocator;

/// <summary>
/// Image layout kind
/// </summary>
public enum PEImageLayoutKind {
	/// <summary>
	/// Use this if the PE file has a normal structure (eg. it's been read from a file on disk)
	/// </summary>
	Flat,

	/// <summary>
	/// Use this if the PE file has been loaded into memory by the OS PE file loader
	/// </summary>
	Mapped,

	/// <summary>
	/// Use this if the PE file has been loaded into memory by the OS PE file loader
	/// </summary>
	Loaded
}

/// <summary>
/// CLR internal image layout
/// </summary>
public sealed class PEImageLayout {
	/// <summary>
	/// Layout kind
	/// </summary>
	public PEImageLayoutKind Kind;

	/// <summary>
	/// Image base address
	/// </summary>
	public nuint ImageBase;

	/// <summary>
	/// Image size (size of file on disk, as opposed to OptionalHeaders.SizeOfImage)
	/// </summary>
	public uint ImageSize;

	/// <summary>
	/// Address of <see cref="RuntimeDefinitions.IMAGE_COR20_HEADER"/>
	/// </summary>
	public nuint CorHeaderAddress;
}

/// <summary>
/// .NET PE Info
/// </summary>
public sealed class DotNetPEInfo {
	/// <summary>
	/// Empty instance
	/// </summary>
	public static readonly DotNetPEInfo Empty = new();

	/// <summary>
	/// Determine if current instance is invalid
	/// </summary>
	public bool IsInvalid => this == Empty;

	/// <summary>
	/// ImageLayout
	/// </summary>
	public PEImageLayout[] ImageLayouts = Array2.Empty<PEImageLayout>();

	/// <summary>
	/// Image file path
	/// </summary>
	public string FilePath = string.Empty;

	/// <summary>
	/// If image is loaded from file
	/// </summary>
	public bool IsFile => !IsMemory;

	/// <summary>
	/// If image is loaded in memory
	/// </summary>
	public bool IsMemory => string.IsNullOrEmpty(FilePath);

	/// <summary>
	/// Get the .NET PE info of a module
	/// </summary>
	/// <param name="module"></param>
	/// <returns></returns>
	public static DotNetPEInfo Create(Module module) {
		return DotNetPEInfoImpl.GetDotNetPEInfo(module);
	}
}
