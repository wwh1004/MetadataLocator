using System;
using System.Diagnostics;
using System.Reflection;

namespace MetadataLocator;

static unsafe class MetadataInfoImpl {
	sealed class PointerProfile {
		public nuint Vfptr; // MDInternalRO or MDInternalRW
		public Pointer Schema = Pointer.Empty;
		public Pointer MetadataAddress = Pointer.Empty;
		public Pointer MetadataSize = Pointer.Empty;
		public Pointer[] HeapAddress = Array2.Empty<Pointer>();
		public Pointer[] HeapSize = Array2.Empty<Pointer>();
	}

	const int StringHeapIndex = 0;
	const int UserStringsHeapIndex = 1;
	const int GuidHeapIndex = 2;
	const int BlobHeapIndex = 3;

	static PointerProfile[] profiles = Array2.Empty<PointerProfile>();
	static bool isInitialized;

	static void Initialize() {
		if (isInitialized)
			return;

		profiles = new PointerProfile[2];
		profiles[0] = CreateProfile(false);
		profiles[1] = CreateProfile(true);

		isInitialized = true;
	}

	static PointerProfile CreateProfile(bool uncompressed) {
		const bool InMemory = false;

		var assemblyFlags = InMemory ? TestAssemblyFlags.InMemory : 0;
		if (uncompressed)
			assemblyFlags |= TestAssemblyFlags.Uncompressed;
		var assembly = TestAssemblyManager.GetAssembly(assemblyFlags);
		nuint module = assembly.ModuleHandle;
		Utils.Check((RuntimeDefinitions.Module*)module, assembly.Module.Assembly.GetName().Name);
		// Get native Module object

		var stgdbPointer = ScanLiteWeightStgdbPointer(uncompressed, out nuint vfptr);
		ScanMetadataOffsets(stgdbPointer, uncompressed, out uint metadataAddressOffset, out uint metadataSizeOffset);
		var info = new MiniMetadataInfo(Utils.ReadUIntPtr(Utils.WithOffset(stgdbPointer, metadataAddressOffset), module));
		ScanSchemaOffset(stgdbPointer, info, uncompressed, out uint schemaOffset);
		ScanHeapOffsets(stgdbPointer, info, uncompressed, out var heapAddressOffsets, out var heapSizeOffsets);
		var profile = new PointerProfile {
			Vfptr = vfptr,
			Schema = Utils.WithOffset(stgdbPointer, schemaOffset),
			MetadataAddress = Utils.WithOffset(stgdbPointer, metadataAddressOffset),
			MetadataSize = Utils.WithOffset(stgdbPointer, metadataSizeOffset),
			HeapAddress = Utils.WithOffset(stgdbPointer, heapAddressOffsets),
			HeapSize = Utils.WithOffset(stgdbPointer, heapSizeOffsets),
		};
		return profile;
	}

	// Not really a CLiteWeightStgdb, for compressed metadata it returens
	static Pointer ScanLiteWeightStgdbPointer(bool uncompressed, out nuint vfptr) {
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

		uint m_pStgdb_Offset = 0;
		if (uncompressed) {
			if (RuntimeEnvironment.Version >= RuntimeVersion.Fx45)
				m_pStgdb_Offset = (uint)((nuint)(&((RuntimeDefinitions.MDInternalRW_45*)&dummy)->m_pStgdb) - (nuint)(&dummy));
			else
				m_pStgdb_Offset = (uint)((nuint)(&((RuntimeDefinitions.MDInternalRW_20*)&dummy)->m_pStgdb) - (nuint)(&dummy));
		}
		// MDInternalRW.m_pStgdb

		var pointer = new Pointer(new[] {
			m_file_Offset,
			m_pMDImport_Offset
		});
		if (m_pStgdb_Offset != 0)
			pointer.Add(m_pStgdb_Offset);
		Utils.Check(Utils.Verify(pointer, uncompressed, p => Memory.TryReadUInt32(p, out _)));
		return pointer;
	}

	static void ScanMetadataOffsets(Pointer stgdbPointer, bool uncompressed, out uint metadataAddressOffset, out uint metadataSizeOffset) {
		const bool InMemory = false;

		var assemblyFlags = InMemory ? TestAssemblyFlags.InMemory : 0;
		if (uncompressed)
			assemblyFlags |= TestAssemblyFlags.Uncompressed;
		var assembly = TestAssemblyManager.GetAssembly(assemblyFlags);
		nuint module = assembly.ModuleHandle;
		Utils.Check((RuntimeDefinitions.Module*)module, assembly.Module.Assembly.GetName().Name);
		// Get native Module object

		nuint pStgdb = Utils.ReadUIntPtr(stgdbPointer, module);
		var peInfo = PEInfo.Create(assembly.Module);
		var imageLayout = peInfo.MappedLayout.IsInvalid ? peInfo.LoadedLayout : peInfo.MappedLayout;
		var m_pCorHeader = (RuntimeDefinitions.IMAGE_COR20_HEADER*)imageLayout.CorHeaderAddress;
		nuint m_pvMd = imageLayout.ImageBase + m_pCorHeader->MetaData.VirtualAddress;
		uint m_cbMd = uncompressed ? 0x1c : m_pCorHeader->MetaData.Size;
		// *pcb = sizeof(STORAGESIGNATURE) + pStorage->GetVersionStringLength();
		// TODO: we should calculate actual metadata size for uncompressed metadata
		uint start = uncompressed ? (sizeof(nuint) == 4 ? 0x1000u : 0x19A0) : (sizeof(nuint) == 4 ? 0x350u : 0x5B0);
		uint end = uncompressed ? (sizeof(nuint) == 4 ? 0x1200u : 0x1BA0) : (sizeof(nuint) == 4 ? 0x39Cu : 0x5FC);
		uint m_pvMd_Offset = 0;
		for (uint offset = start; offset <= end; offset += 4) {
			if (*(nuint*)(pStgdb + offset) != m_pvMd)
				continue;
			if (*(uint*)(pStgdb + offset + (uint)sizeof(nuint)) != m_cbMd)
				continue;
			m_pvMd_Offset = offset;
			break;
		}
		Utils.Check(m_pvMd_Offset != 0);

		Utils.Check(Utils.Verify(Utils.WithOffset(stgdbPointer, m_pvMd_Offset), uncompressed, p => Memory.TryReadUInt32(p, out uint signature) && signature == 0x424A5342));
		metadataAddressOffset = m_pvMd_Offset;
		metadataSizeOffset = m_pvMd_Offset + (uint)sizeof(nuint);
	}

	static void ScanSchemaOffset(Pointer stgdbPointer, MiniMetadataInfo info, bool uncompressed, out uint schemaOffset) {
		const bool InMemory = false;

		var assemblyFlags = InMemory ? TestAssemblyFlags.InMemory : 0;
		if (uncompressed)
			assemblyFlags |= TestAssemblyFlags.Uncompressed;
		var assembly = TestAssemblyManager.GetAssembly(assemblyFlags);
		nuint module = assembly.ModuleHandle;
		Utils.Check((RuntimeDefinitions.Module*)module, assembly.Module.Assembly.GetName().Name);
		// Get native Module object

		nuint pStgdb = Utils.ReadUIntPtr(stgdbPointer, module);
		for (schemaOffset = 0; schemaOffset < 0x30; schemaOffset += 4) {
			if (*(ulong*)(pStgdb + schemaOffset) != info.Header1)
				continue;
			if (*(ulong*)(pStgdb + schemaOffset + 0x08) != info.ValidMask)
				continue;
			if (*(ulong*)(pStgdb + schemaOffset + 0x10) != info.SortedMask)
				continue;
			break;
		}
		Utils.Check(schemaOffset != 0x30);
	}

	static void ScanHeapOffsets(Pointer stgdbPointer, MiniMetadataInfo info, bool uncompressed, out uint[] heapAddressOffsets, out uint[] heapSizeOffsets) {
		const bool InMemory = false;

		var assemblyFlags = InMemory ? TestAssemblyFlags.InMemory : 0;
		if (uncompressed)
			assemblyFlags |= TestAssemblyFlags.Uncompressed;
		var assembly = TestAssemblyManager.GetAssembly(assemblyFlags);
		nuint module = assembly.ModuleHandle;
		Utils.Check((RuntimeDefinitions.Module*)module, assembly.Module.Assembly.GetName().Name);
		// Get native Module object

		nuint pStgdb = Utils.ReadUIntPtr(stgdbPointer, module);
		uint start = uncompressed ? (sizeof(nuint) == 4 ? 0xD00u : 0x1500) : (sizeof(nuint) == 4 ? 0x2A0u : 0x500);
		uint end = uncompressed ? (sizeof(nuint) == 4 ? 0x1000u : 0x1900) : (sizeof(nuint) == 4 ? 0x3A0u : 0x600);
		heapAddressOffsets = new uint[4];
		heapSizeOffsets = new uint[heapAddressOffsets.Length];
		int found = 0;
		for (uint offset = start; offset < end; offset += 4) {
			nuint address = *(nuint*)(pStgdb + offset);
			uint size = *(uint*)(pStgdb + offset + (2 * (uint)sizeof(nuint)));
			if (address == info.StringHeapAddress) {
				Utils.Check(info.StringHeapSize - 8 < size && size <= info.StringHeapSize);
				Utils.Check(heapAddressOffsets[0] == 0);
				heapAddressOffsets[StringHeapIndex] = offset;
				heapSizeOffsets[StringHeapIndex] = offset + (2 * (uint)sizeof(nuint));
				found++;
			}
			else if (address == info.UserStringHeapAddress) {
				Utils.Check(info.UserStringHeapSize - 8 < size && size <= info.UserStringHeapSize);
				Utils.Check(heapAddressOffsets[1] == 0);
				heapAddressOffsets[UserStringsHeapIndex] = offset;
				heapSizeOffsets[UserStringsHeapIndex] = offset + (2 * (uint)sizeof(nuint));
				found++;
			}
			else if (address == info.GuidHeapAddress) {
				Utils.Check(info.GuidHeapSize - 8 < size && size <= info.GuidHeapSize);
				Utils.Check(heapAddressOffsets[2] == 0);
				heapAddressOffsets[GuidHeapIndex] = offset;
				heapSizeOffsets[GuidHeapIndex] = offset + (2 * (uint)sizeof(nuint));
				found++;
			}
			else if (address == info.BlobHeapAddress) {
				Utils.Check(info.BlobHeapSize - 8 < size && size <= info.BlobHeapSize);
				Utils.Check(heapAddressOffsets[3] == 0);
				heapAddressOffsets[BlobHeapIndex] = offset;
				heapSizeOffsets[BlobHeapIndex] = offset + (2 * (uint)sizeof(nuint));
				found++;
			}
		}
		Utils.Check(found == 4);
		// Find heeap info offsets

		for (int i = 0; i < heapAddressOffsets.Length; i++)
			Utils.Check(Utils.Verify(Utils.WithOffset(stgdbPointer, heapAddressOffsets[i]), uncompressed, p => Memory.TryReadUInt32(p, out _)));
	}

	public static MetadataInfo GetMetadataInfo(Module module) {
		if (module is null)
			throw new ArgumentNullException(nameof(module));

		Initialize();
		nuint moduleHandle = ReflectionHelpers.GetModuleHandle(module);
		nuint vfptr = MetadataImport.Create(module).Vfptr;
		foreach (var profile in profiles) {
			if (vfptr != profile.Vfptr)
				continue;

			var metadataInfo = new MetadataInfo {
				MetadataAddress = Utils.ReadUIntPtr(profile.MetadataAddress, moduleHandle),
				MetadataSize = Utils.ReadUInt32(profile.MetadataSize, moduleHandle),
				Schema = GetSchema(profile, moduleHandle),
				TableStream = GetTableStream(),
				StringHeap = GetHeapInfo(profile, StringHeapIndex, moduleHandle),
				UserStringHeap = GetHeapInfo(profile, UserStringsHeapIndex, moduleHandle),
				GuidHeap = GetHeapInfo(profile, GuidHeapIndex, moduleHandle),
				BlobHeap = GetHeapInfo(profile, BlobHeapIndex, moduleHandle)
			};
			return metadataInfo;
		}
		Debug2.Assert(false);
		return MetadataInfo.Empty;
	}

	static MetadataSchema GetSchema(PointerProfile profile, nuint moduleHandle) {
		var pSchema = (RuntimeDefinitions.CMiniMdSchema*)Utils.ReadPointer(profile.Schema, moduleHandle);
		if (pSchema is null) {
			Debug2.Assert(false);
			return MetadataSchema.Empty;
		}
		var rows = new uint[RuntimeDefinitions.TBL_COUNT];
		for (int i = 0; i < rows.Length; i++)
			rows[i] = pSchema->m_cRecs[i];
		var schema = new MetadataSchema {
			Reserved1 = pSchema->__base.m_ulReserved,
			MajorVersion = pSchema->__base.m_major,
			MinorVersion = pSchema->__base.m_minor,
			Log2Rid = pSchema->__base.m_rid,
			Flags = pSchema->__base.m_heaps,
			ValidMask = pSchema->__base.m_maskvalid,
			SortedMask = pSchema->__base.m_sorted,
			Rows = rows,
			ExtraData = pSchema->m_ulExtra
		};
		return schema;
	}

	static MetadataTableInfo GetTableStream() {
		return MetadataTableInfo.Empty;
	}

	static MetadataHeapInfo GetHeapInfo(PointerProfile profile, int index, nuint moduleHandle) {
		uint size = Utils.ReadUInt32(profile.HeapSize[index], moduleHandle);
		if (size == 0)
			return MetadataHeapInfo.Empty;
		// TODO: also check m_pSegData is pointer to m_zeros
		var heapInfo = new MetadataHeapInfo {
			Address = Utils.ReadUIntPtr(profile.HeapAddress[index], moduleHandle),
			Length = size
		};
		return heapInfo;
	}

	sealed class MiniMetadataInfo {
		public ulong Header1;
		public ulong ValidMask;
		public ulong SortedMask;
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
				case "#-":
					Utils.Check(TableStreamAddress == 0);
					TableStreamAddress = pMetadata + offset;
					TableStreamSize = size;
					break;
				case "#Strings":
					Utils.Check(StringHeapAddress == 0);
					StringHeapAddress = pMetadata + offset;
					StringHeapSize = size;
					break;
				case "#US":
					Utils.Check(UserStringHeapAddress == 0);
					UserStringHeapAddress = pMetadata + offset;
					UserStringHeapSize = size;
					break;
				case "#GUID":
					Utils.Check(GuidHeapAddress == 0);
					GuidHeapAddress = pMetadata + offset;
					GuidHeapSize = size;
					break;
				case "#Blob":
					Utils.Check(BlobHeapAddress == 0);
					BlobHeapAddress = pMetadata + offset;
					BlobHeapSize = size;
					break;
				default:
					Debug2.Assert(false);
					throw new NotSupportedException();
				}
			}
			Header1 = *(ulong*)TableStreamAddress;
			ValidMask = *(ulong*)(TableStreamAddress + 0x08);
			SortedMask = *(ulong*)(TableStreamAddress + 0x10);
		}
	}
}
