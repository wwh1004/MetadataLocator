using System.Reflection;

namespace MetadataLocator;

/// <summary>
/// Image layout
/// </summary>
public enum ImageLayout {
	/// <summary>
	/// Use this if the PE file has a normal structure (eg. it's been read from a file on disk)
	/// </summary>
	File,

	/// <summary>
	/// Use this if the PE file has been loaded into memory by the OS PE file loader
	/// </summary>
	Memory
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
	public ImageLayout ImageLayout;

	/// <summary>
	/// Address of COR20_HEADER
	/// </summary>
	public nuint Cor20HeaderAddress;

	/// <summary>
	/// Address of metadata
	/// </summary>
	public nuint MetadataAddress;

	/// <summary>
	/// Size of metadata
	/// </summary>
	public uint MetadataSize;

	/// <summary>
	/// Get the .NET PE info of a module
	/// </summary>
	/// <param name="module"></param>
	/// <returns></returns>
	public static DotNetPEInfo Create(Module module) {
		return DotNetPEInfoImpl.GetDotNetPEInfo(module);
	}
}
