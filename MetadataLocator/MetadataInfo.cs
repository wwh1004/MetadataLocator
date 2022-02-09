using System.Reflection;

namespace MetadataLocator;

/// <summary>
/// Metadata stream info
/// </summary>
public abstract class MetadataStreamInfo {
	/// <summary>
	/// Determine if current instance is empty
	/// </summary>
	public bool IsEmpty => Address == 0 || Length == 0;

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
	/// Address of metadata
	/// </summary>
	public nuint MetadataAddress;

	/// <summary>
	/// Size of metadata
	/// </summary>
	/// <remarks>Currently return 0 if table stream is uncompressed (aka #-)</remarks>
	public uint MetadataSize;

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
	/// Get the metadata info of a module
	/// </summary>
	/// <param name="module"></param>
	/// <returns></returns>
	public static MetadataInfo Create(Module module) {
		return MetadataInfoImpl.GetMetadataInfo(module);
	}
}
