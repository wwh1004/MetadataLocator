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
/// Metadata stream info
/// </summary>
public abstract class MetadataStreamInfo {
	/// <summary>
	/// Address of stream
	/// </summary>
	public nuint Address;

	/// <summary>
	/// Length of stream
	/// </summary>
	public uint Length;
}

/// <summary>
/// Metadata table info (#~, #-)
/// </summary>
public sealed class MetadataTableInfo : MetadataStreamInfo {
	/// <summary>
	/// Empty instance
	/// </summary>
	public static readonly MetadataTableInfo Empty = new();

	/// <summary>
	/// Determine if current instance is invalid
	/// </summary>
	public bool IsInvalid => this == Empty;

	/// <summary>
	/// Is compressed table stream (#~)
	/// </summary>
	public bool IsCompressed;
}

/// <summary>
/// Metadata heap info (#Strings, #US, #GUID, #Blob)
/// </summary>
public sealed class MetadataHeapInfo : MetadataStreamInfo {
	/// <summary>
	/// Empty instance
	/// </summary>
	public static readonly MetadataHeapInfo Empty = new();

	/// <summary>
	/// Determine if current instance is invalid
	/// </summary>
	public bool IsInvalid => this == Empty;
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

/// <summary>
/// Metadata info
/// </summary>
public sealed class MetadataInfo {
	/// <summary>
	/// Empty instance
	/// </summary>
	public static readonly MetadataInfo Empty = new();

	/// <summary>
	/// Determine if current instance is invalid
	/// </summary>
	public bool IsInvalid => this == Empty;

	/// <summary>
	/// The instance of <see cref="MetadataLocator.MetadataImport"/>
	/// </summary>
	public MetadataImport MetadataImport = MetadataImport.Empty;

	/// <summary>
	/// #~ or #- info
	/// </summary>
	public MetadataTableInfo TableStream = MetadataTableInfo.Empty;

	/// <summary>
	/// #Strings heap info
	/// </summary>
	public MetadataHeapInfo StringHeap = MetadataHeapInfo.Empty;

	/// <summary>
	/// #US heap info
	/// </summary>
	public MetadataHeapInfo UserStringHeap = MetadataHeapInfo.Empty;

	/// <summary>
	/// #GUID heap info
	/// </summary>
	public MetadataHeapInfo GuidHeap = MetadataHeapInfo.Empty;

	/// <summary>
	/// #Blob heap info
	/// </summary>
	public MetadataHeapInfo BlobHeap = MetadataHeapInfo.Empty;

	/// <summary>
	/// .NET PE Info
	/// </summary>
	public DotNetPEInfo PEInfo = DotNetPEInfo.Empty;

	/// <summary>
	/// Get the metadata info of a module
	/// </summary>
	/// <param name="module"></param>
	/// <returns></returns>
	public static MetadataInfo Create(Module module) {
		return MetadataInfoImpl.GetMetadataInfo(module);
	}
}
