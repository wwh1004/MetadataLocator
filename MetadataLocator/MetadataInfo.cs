using System.Reflection;

namespace MetadataLocator {
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
	public sealed unsafe class MetadataStreamInfo {
		/// <summary>
		/// Address of stream
		/// </summary>
		public void* Address;

		/// <summary>
		/// Length of stream
		/// </summary>
		public uint Length;
	}

	/// <summary>
	/// .NET PE Info
	/// </summary>
	public sealed unsafe class DotNetPEInfo {
		/// <summary>
		/// Determine if current instance is valid
		/// </summary>
		public bool IsValid;

		/// <summary>
		/// ImageLayout
		/// </summary>
		public ImageLayout ImageLayout;

		/// <summary>
		/// Address of COR20_HEADER
		/// </summary>
		public void* Cor20HeaderAddress;

		/// <summary>
		/// Address of metadata
		/// </summary>
		public void* MetadataAddress;

		/// <summary>
		/// Size of metadata
		/// </summary>
		public uint MetadataSize;
	}

	/// <summary>
	/// Metadata info
	/// </summary>
	public sealed class MetadataInfo {
		/// <summary>
		/// Module
		/// </summary>
		public Module Module;

		/// <summary>
		/// The instance of <see cref="IMetaDataTables"/>
		/// </summary>
		public IMetaDataTables MetaDataTables;

		/// <summary>
		/// #~ or #- info
		/// </summary>
		public MetadataStreamInfo TableStream;

		/// <summary>
		/// #Strings heap info
		/// </summary>
		public MetadataStreamInfo StringHeap;

		/// <summary>
		/// #US heap info
		/// </summary>
		public MetadataStreamInfo UserStringHeap;

		/// <summary>
		/// #GUID heap info
		/// </summary>
		public MetadataStreamInfo GuidHeap;

		/// <summary>
		/// #Blob heap info
		/// </summary>
		public MetadataStreamInfo BlobHeap;

		/// <summary>
		/// .NET PE Info (invalid if PEInfo.IsNativeImage is true)
		/// </summary>
		public DotNetPEInfo PEInfo;

		/// <summary>
		/// Get the metadata info of a module
		/// </summary>
		/// <param name="module"></param>
		/// <returns></returns>
		public static MetadataInfo GetMetadataInfo(Module module) {
			return MetadataInfoImpl.GetMetadataInfo(module);
		}
	}
}
