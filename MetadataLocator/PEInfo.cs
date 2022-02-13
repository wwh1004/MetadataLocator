using System.Reflection;

namespace MetadataLocator;

/// <summary>
/// CLR internal image layout
/// </summary>
public sealed class PEImageLayout {
	/// <summary>
	/// Determine if current instance is invalid
	/// </summary>
	public bool IsInvalid => ImageBase == 0;

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
public sealed class PEInfo {
	/// <summary>
	/// Determine if current instance is invalid
	/// </summary>
	public bool IsInvalid => LoadedLayout.IsInvalid;

	/// <summary>
	/// Image file path
	/// </summary>
	public string FilePath = string.Empty;

	/// <summary>
	/// If image is loaded in memory
	/// </summary>
	public bool InMemory => string.IsNullOrEmpty(FilePath);

	/// <summary>
	/// Flat image layout, maybe empty (Assembly.Load(byte[]))
	/// </summary>
	public PEImageLayout FlatLayout = new();

	/// <summary>
	/// Mapped image layout, maybe empty (Assembly.LoadFile)
	/// </summary>
	public PEImageLayout MappedLayout = new();

	/// <summary>
	/// Loaded image layout, not empty (Assembly.LoadFile)
	/// </summary>
	public PEImageLayout LoadedLayout = new();

	/// <summary>
	/// Get the .NET PE info of a module
	/// </summary>
	/// <param name="module"></param>
	/// <returns></returns>
	public static PEInfo Create(Module module) {
		return PEInfoImpl.GetPEInfo(module);
	}
}
