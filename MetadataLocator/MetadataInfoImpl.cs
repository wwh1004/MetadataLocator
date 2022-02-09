using System;
using System.Diagnostics;
using System.Reflection;

namespace MetadataLocator;

static unsafe class MetadataInfoImpl {
	static nuint metadataInterfaceVfptr_RO;
	static Pointer metadataAddressPointer_RO = Pointer.Empty;
	static Pointer metadataSizePointer_RO = Pointer.Empty;
	static nuint metadataInterfaceVfptr_RW;
	static Pointer metadataAddressPointer_RW = Pointer.Empty;
	static Pointer metadataSizePointer_RW = Pointer.Empty;
	static bool isInitialized;

	static void Initialize() {
		if (isInitialized)
			return;

		metadataAddressPointer_RO = ScanMetadataAddressPointer(false, out metadataInterfaceVfptr_RO);
		Debug2.Assert(!metadataAddressPointer_RO.IsEmpty);
		metadataSizePointer_RO = new Pointer(metadataAddressPointer_RO);
		metadataSizePointer_RO.Offsets[metadataSizePointer_RO.Offsets.Count - 1] += (uint)sizeof(nuint);
		ScanHeapInfoPointers(metadataAddressPointer_RO, false);

		metadataAddressPointer_RW = ScanMetadataAddressPointer(true, out metadataInterfaceVfptr_RW);
		Debug2.Assert(!metadataAddressPointer_RW.IsEmpty);
		metadataSizePointer_RW = new Pointer(metadataAddressPointer_RW);
		metadataSizePointer_RW.Offsets[metadataSizePointer_RW.Offsets.Count - 1] += (uint)sizeof(nuint);



		isInitialized = true;
	}

	static Pointer ScanMetadataAddressPointer(bool uncompressed, out nuint vfptr) {
		const bool InMemory = false;

		int dummy = 0;
		var assemblyFlags = InMemory ? TestAssemblyFlags.InMemory : 0;
		if (uncompressed)
			assemblyFlags |= TestAssemblyFlags.Uncompressed;
		var assembly = TestAssemblyManager.GetAssembly(assemblyFlags);
		nuint module = assembly.ModuleHandle;
		Utils.Check((RuntimeDefinitions.Module*)module, assembly.Module.Assembly.GetName().Name);
		// Get native Module object

		uint m_file_Offset;
		if (RuntimeEnvironment.Version >= RuntimeVersion.Fx453)
			m_file_Offset = (uint)((nuint)(&((RuntimeDefinitions.Module_453*)&dummy)->m_file) - (nuint)(&dummy));
		else
			m_file_Offset = (uint)((nuint)(&((RuntimeDefinitions.Module_20*)&dummy)->m_file) - (nuint)(&dummy));
		nuint m_file = *(nuint*)(module + m_file_Offset);
		Utils.Check((RuntimeDefinitions.PEFile*)m_file);
		// Module.m_file

		var metadataImport = MetadataImport.Create(assembly.Module);
		vfptr = metadataImport.Vfptr;
		nuint m_pMDImport = metadataImport.This;
		uint m_pMDImport_Offset;
		bool found = false;
		for (m_pMDImport_Offset = 0; m_pMDImport_Offset < 8 * (uint)sizeof(nuint); m_pMDImport_Offset += 4) {
			if (*(nuint*)(m_file + m_pMDImport_Offset) != m_pMDImport)
				continue;
			found = true;
			break;
		}
		Utils.Check(found);
		// PEFile.m_pMDImport

		var peInfo = PEInfo.Create(assembly.Module);
		var imageLayout = peInfo.MappedLayout.IsInvalid ? peInfo.LoadedLayout : peInfo.MappedLayout;
		var m_pCorHeader = (RuntimeDefinitions.IMAGE_COR20_HEADER*)imageLayout.CorHeaderAddress;
		nuint m_pvMd = imageLayout.ImageBase + m_pCorHeader->MetaData.VirtualAddress;
		Utils.Check((RuntimeDefinitions.STORAGESIGNATURE*)m_pvMd);
		uint m_pStgdb_Offset = 0;
		uint m_pvMd_Offset = 0;
		if (uncompressed) {
			uint m_cbMd = 0x1c;
			// *pcb = sizeof(STORAGESIGNATURE) + pStorage->GetVersionStringLength();
			// TODO: we should calculate actual metadata size
			if (RuntimeEnvironment.Version >= RuntimeVersion.Fx45)
				m_pStgdb_Offset = (uint)((nuint)(&((RuntimeDefinitions.MDInternalRW_45*)&dummy)->m_pStgdb) - (nuint)(&dummy));
			else
				m_pStgdb_Offset = (uint)((nuint)(&((RuntimeDefinitions.MDInternalRW_20*)&dummy)->m_pStgdb) - (nuint)(&dummy));
			nuint m_pStgdb = *(nuint*)(m_pMDImport + m_pStgdb_Offset);
			uint start = sizeof(nuint) == 4 ? 0x1000u : 0x19A0;
			uint end = sizeof(nuint) == 4 ? 0x1200u : 0x1BA0;
			for (uint offset = start; offset <= end; offset += 4) {
				if (*(nuint*)(m_pStgdb + offset) != m_pvMd)
					continue;
				if (*(uint*)(m_pStgdb + offset + (uint)sizeof(nuint)) != m_cbMd)
					continue;
				m_pvMd_Offset = offset;
				break;
			}
		}
		else {
			uint m_cbMd = m_pCorHeader->MetaData.Size;
			uint start = sizeof(nuint) == 4 ? 0x350u : 0x5B0;
			uint end = sizeof(nuint) == 4 ? 0x39Cu : 0x5FC;
			for (uint offset = start; offset <= end; offset += 4) {
				if (*(nuint*)(m_pMDImport + offset) != m_pvMd)
					continue;
				if (*(uint*)(m_pMDImport + offset + (uint)sizeof(nuint)) != m_cbMd)
					continue;
				m_pvMd_Offset = offset;
				break;
			}

			var m_GuidHeap = *(RuntimeDefinitions.GuidHeapRO*)(m_pMDImport + m_pvMd_Offset - (uint)sizeof(RuntimeDefinitions.StgPoolReadOnly));
			var m_UserStringHeap = *(RuntimeDefinitions.BlobHeapRO*)(m_pMDImport + m_pvMd_Offset - 2 * (uint)sizeof(RuntimeDefinitions.StgPoolReadOnly));
			var m_BlobHeap = *(RuntimeDefinitions.BlobHeapRO*)(m_pMDImport + m_pvMd_Offset - 3 * (uint)sizeof(RuntimeDefinitions.StgPoolReadOnly));
			var m_StringHeap = *(RuntimeDefinitions.StringHeapRO*)(m_pMDImport + m_pvMd_Offset - 4 * (uint)sizeof(RuntimeDefinitions.StgPoolReadOnly));
		}
		Utils.Check(m_pvMd_Offset != 0);

		var pointer = new Pointer(new[] {
			m_file_Offset,
			m_pMDImport_Offset
		});
		if (m_pStgdb_Offset != 0)
			pointer.Add(m_pStgdb_Offset);
		pointer.Add(m_pvMd_Offset);
		{
			var a = (RuntimeDefinitions.MDInternalRO_20*)m_pMDImport;
			var b = (RuntimeDefinitions.MDInternalRO_45*)m_pMDImport;
		}
		Utils.Check(Utils.Verify(pointer, uncompressed, p => Memory.TryReadUInt32(p, out uint signature) && signature == 0x424A5342));
		return pointer;
	}

	static Pointer[] ScanHeapInfoPointers(Pointer metadataAddressPointer, bool uncompressed) {
		const bool InMemory = false;

		var assemblyFlags = InMemory ? TestAssemblyFlags.InMemory : 0;
		if (uncompressed)
			assemblyFlags |= TestAssemblyFlags.Uncompressed;
		var assembly = TestAssemblyManager.GetAssembly(assemblyFlags);
		nuint module = assembly.ModuleHandle;
		Utils.Check((RuntimeDefinitions.Module*)module, assembly.Module.Assembly.GetName().Name);
		// Get native Module object

		nuint pMetadata = Utils.ReadUIntPtr(metadataAddressPointer, module);
		var info = new MiniMetadataInfo(pMetadata);
		if (uncompressed) {
			return null;
		}
		else {
			var guidHeapPointer = new Pointer(metadataAddressPointer);
			guidHeapPointer.Offsets[guidHeapPointer.Offsets.Count - 1] -= (uint)sizeof(RuntimeDefinitions.StgPoolReadOnly);
			Utils.Check(Utils.ReadUIntPtr(guidHeapPointer, module) == info.GuidHeapAddress);
			// m_GuidHeap

			var userStringHeapPointer = new Pointer(guidHeapPointer);
			userStringHeapPointer.Offsets[userStringHeapPointer.Offsets.Count - 1] -= (uint)sizeof(RuntimeDefinitions.StgPoolReadOnly);
			Utils.Check(Utils.ReadUIntPtr(userStringHeapPointer, module) == info.UserStringHeapAddress);
			// m_UserStringHeap

			var blobHeapPointer = new Pointer(userStringHeapPointer);
			blobHeapPointer.Offsets[blobHeapPointer.Offsets.Count - 1] -= (uint)sizeof(RuntimeDefinitions.StgPoolReadOnly);
			Utils.Check(Utils.ReadUIntPtr(blobHeapPointer, module) == info.BlobHeapAddress);
			// m_BlobHeap

			var stringHeapPointer = new Pointer(blobHeapPointer);
			stringHeapPointer.Offsets[stringHeapPointer.Offsets.Count - 1] -= (uint)sizeof(RuntimeDefinitions.StgPoolReadOnly);
			Utils.Check(Utils.ReadUIntPtr(stringHeapPointer, module) == info.StringHeapAddress);
			// m_StringHeap

			return new[] { stringHeapPointer, userStringHeapPointer, guidHeapPointer, blobHeapPointer };
		}
	}

	public static MetadataInfo GetMetadataInfo(Module module) {
		if (module is null)
			throw new ArgumentNullException(nameof(module));

		Initialize();
		nuint moduleHandle = ReflectionHelpers.GetModuleHandle(module);
		nuint vfptr = MetadataImport.Create(module).Vfptr;
		if (vfptr == metadataInterfaceVfptr_RO) {
			var metadataInfo = new MetadataInfo();
			metadataInfo.MetadataAddress = Utils.ReadUIntPtr(metadataAddressPointer_RO, moduleHandle);
			metadataInfo.MetadataSize = Utils.ReadUInt32(metadataSizePointer_RO, moduleHandle);
			metadataInfo.TableStream = GetTableStream(metadataInfo);
			metadataInfo.StringHeap = GetStringHeap(metadataInfo);
			metadataInfo.UserStringHeap = GetUserStringHeap(metadataInfo);
			metadataInfo.GuidHeap = GetGuidHeap(metadataInfo);
			metadataInfo.BlobHeap = GetBlobHeap(metadataInfo);
		}
		else if (vfptr == metadataInterfaceVfptr_RW) {
			Utils.ReadUIntPtr(metadataAddressPointer_RW, moduleHandle);
			Utils.ReadUInt32(metadataSizePointer_RW, moduleHandle);
			
		}
		else {
			Debug2.Assert(false);
			return MetadataInfo.Empty;
		}
		return MetadataInfo.Empty;
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

	static bool IsZeros(nuint pStgPoolReadOnly) {
		var stgPool = (RuntimeDefinitions.StgPoolReadOnly*)pStgPoolReadOnly;
		return stgPool->__base.m_cbSegSize == 0;
		// TODO: check m_pSegData is pointer to m_zeros
	}

	sealed class MiniMetadataInfo {
		public nuint TableStreamAddress;
		public uint TableStreamSize;
		public nuint StringHeapAddress;
		public uint StringHeapSize;
		public nuint UserStringHeapAddress;
		public uint UserStringHeapSize;
		public nuint GuidHeapAddress;
		public uint GuidHeapSize;
		public nuint BlobHeapAddress;
		public uint BlobHeapSize;

		public MiniMetadataInfo(nuint pMetadata) {
			var pStorageSignature = (RuntimeDefinitions.STORAGESIGNATURE*)pMetadata;
			Utils.Check(pStorageSignature);
			var pStorageHeader = (RuntimeDefinitions.STORAGEHEADER*)((nuint)pStorageSignature + 0x10 + pStorageSignature->iVersionString);
			nuint p = (nuint)pStorageHeader + (uint)sizeof(RuntimeDefinitions.STORAGEHEADER);
			Utils.Check(pStorageHeader->iStreams == 5);
			// must have 5 streams so we can get all stream info offsets
			for (int i = 0; i < pStorageHeader->iStreams; i++) {
				uint offset = *(uint*)p;
				p += 4;
				uint size = *(uint*)p;
				p += 4;
				Utils.Check(Memory.TryReadAnsiString(p, out var name));
				p += ((uint)name.Length + 1 + 3) & ~3u;
				switch (name) {
				case "#~":
					TableStreamAddress = pMetadata + offset;
					TableStreamSize = size;
					break;
				case "#Strings":
					StringHeapAddress = pMetadata + offset;
					StringHeapSize = size;
					break;
				case "#US":
					UserStringHeapAddress = pMetadata + offset;
					UserStringHeapSize = size;
					break;
				case "#GUID":
					GuidHeapAddress = pMetadata + offset;
					GuidHeapSize = size;
					break;
				case "#Blob":
					BlobHeapAddress = pMetadata + offset;
					BlobHeapSize = size;
					break;
				default:
					Debug2.Assert(false);
					throw new NotSupportedException();
				}
			}
		}
	}
}
