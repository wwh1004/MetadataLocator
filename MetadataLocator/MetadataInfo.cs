using System;
using System.Reflection;

namespace MetadataLocator;

/// <summary>
/// Metadata schema
/// </summary>
public sealed class MetadataSchema {
	/// <summary>
	/// Determine if current instance is invalid
	/// </summary>
	public bool IsInvalid => ValidMask == 0;

	/// <summary/>
	public uint Reserved1;

	/// <summary/>
	public byte MajorVersion;

	/// <summary/>
	public byte MinorVersion;

	/// <summary/>
	public byte Flags;

	/// <summary/>
	public byte Log2Rid;

	/// <summary/>
	public ulong ValidMask;

	/// <summary/>
	public ulong SortedMask;

	/// <summary/>
	/// <remarks>Array length always equals to <see cref="RuntimeDefinitions.TBL_COUNT"/> if not empty</remarks>
	public uint[] RowCounts = Array2.Empty<uint>();

	/// <summary/>
	public uint ExtraData;
}

/// <summary>
/// Metadata stream info
/// </summary>
public abstract class MetadataStreamInfo {
	/// <summary>
	/// Determine if current instance is invalid
	/// </summary>
	public bool IsInvalid => Address == 0;

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
	/// Is compressed table stream (#~)
	/// </summary>
	public bool IsCompressed;

	/// <summary>
	/// Table count, see <see cref="RuntimeDefinitions.TBL_COUNT_V1"/> and  <see cref="RuntimeDefinitions.TBL_COUNT_V2"/>
	/// </summary>
	public uint TableCount;

	/// <summary>
	/// Size of each row
	/// </summary>
	/// <remarks>Array length always equals to <see cref="RuntimeDefinitions.TBL_COUNT"/> if not empty</remarks>
	public uint[] RowSizes = Array2.Empty<uint>();
}

/// <summary>
/// Metadata heap info (#Strings, #US, #GUID, #Blob)
/// </summary>
public sealed class MetadataHeapInfo : MetadataStreamInfo {
}

/// <summary>
/// Metadata info
/// </summary>
public sealed class MetadataInfo {
	/// <summary>
	/// Determine if current instance is invalid
	/// </summary>
	public bool IsInvalid => MetadataAddress == 0;

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
	/// <remarks>Currently return 0x1c if table stream is uncompressed (aka #-)</remarks>
	public uint MetadataSize;

	/// <summary>
	/// Metadata schema
	/// </summary>
	public MetadataSchema Schema = new();

	/// <summary>
	/// #~ or #- info
	/// </summary>
	public MetadataTableInfo TableStream = new();

	/// <summary>
	/// #Strings heap info
	/// </summary>
	public MetadataHeapInfo StringHeap = new();

	/// <summary>
	/// #US heap info
	/// </summary>
	public MetadataHeapInfo UserStringHeap = new();

	/// <summary>
	/// #GUID heap info
	/// </summary>
	public MetadataHeapInfo GuidHeap = new();

	/// <summary>
	/// #Blob heap info
	/// </summary>
	public MetadataHeapInfo BlobHeap = new();

	/// <summary>
	/// Get the metadata info of a module
	/// </summary>
	/// <param name="module"></param>
	/// <returns></returns>
	public static MetadataInfo Create(Module module) {
		return MetadataInfoImpl.GetMetadataInfo(module);
	}
}
