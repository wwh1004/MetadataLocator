using System;
using System.Reflection;

namespace MetadataLocator;

static unsafe class MetadataInfoImpl {
	public static MetadataInfo GetMetadataInfo(Module module) {
		if (module is null)
			throw new ArgumentNullException(nameof(module));

		_ = RuntimeEnvironment.Version;
		throw new NotImplementedException();
		var metadataInfo = new MetadataInfo();
		metadataInfo.TableStream = GetTableStream(metadataInfo);
		metadataInfo.StringHeap = GetStringHeap(metadataInfo);
		metadataInfo.UserStringHeap = GetUserStringHeap(metadataInfo);
		metadataInfo.GuidHeap = GetGuidHeap(metadataInfo);
		metadataInfo.BlobHeap = GetBlobHeap(metadataInfo);
		metadataInfo.PEInfo = DotNetPEInfoImpl.GetDotNetPEInfo(module);
		return metadataInfo;
	}

	static MetadataTableInfo GetTableStream(MetadataInfo metadataInfo) {
		throw new NotImplementedException();
	}

	static MetadataHeapInfo GetStringHeap(MetadataInfo metadataInfo) {
		throw new NotImplementedException();
	}

	static MetadataHeapInfo GetUserStringHeap(MetadataInfo metadataInfo) {
		throw new NotImplementedException();
	}

	static MetadataHeapInfo GetGuidHeap(MetadataInfo metadataInfo) {
		throw new NotImplementedException();
	}

	static MetadataHeapInfo GetBlobHeap(MetadataInfo metadataInfo) {
		throw new NotImplementedException();
	}

	static uint AlignUp(uint value, uint alignment) {
		return (value + alignment - 1) & ~(alignment - 1);
	}

	static byte GetCompressedUInt32Length(uint value) {
		if (value < 0x80)
			return 1;
		else if (value < 0x4000)
			return 2;
		else
			return 4;
	}
}
