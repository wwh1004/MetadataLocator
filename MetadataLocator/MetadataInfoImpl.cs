using System;
using System.Diagnostics;
using System.Reflection;

namespace MetadataLocator;

static unsafe class MetadataInfoImpl {
	sealed class Profile {
		public nuint Vfptr; // MDInternalRO or MDInternalRW
		public bool Uncompressed;
		public bool RidAsIndex;
		public Pointer Schema = Pointer.Empty;
		public Pointer TableCount = Pointer.Empty;
		public Pointer TableDefs = Pointer.Empty;
		public Pointer MetadataAddress = Pointer.Empty;
		public Pointer MetadataSize = Pointer.Empty;
		public Pointer[] HeapAddress = Array2.Empty<Pointer>();
		public Pointer[] HeapSize = Array2.Empty<Pointer>();
		public Pointer TableAddress = Pointer.Empty;
		public uint NextTableOffset;
	}

	const int StringHeapIndex = 0;
	const int UserStringsHeapIndex = 1;
	const int GuidHeapIndex = 2;
	const int BlobHeapIndex = 3;

	static Profile[] profiles = Array2.Empty<Profile>();
	static bool isInitialized;

	static void Initialize() {
		if (isInitialized)
			return;

		profiles = new Profile[2];
		profiles[0] = CreateProfile(false);
		profiles[1] = CreateProfile(true);

		isInitialized = true;
	}

	static Profile CreateProfile(bool uncompressed) {
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
		ScanTableDefsOffsets(stgdbPointer, uncompressed, schemaOffset, out uint tableCountOffset, out uint tableDefsOffset);
		ScanHeapOffsets(stgdbPointer, info, uncompressed, out var heapAddressOffsets, out var heapSizeOffsets);
		bool ridAsIndex = RuntimeEnvironment.Version < RuntimeVersion.Fx40 && !uncompressed;
		// see sscli2 CMiniMd::SetTablePointers, Set the pointers to consecutive areas of a large buffer.
		info.CalculateTableAddress(Utils.ReadPointer(Utils.WithOffset(stgdbPointer, tableDefsOffset), module), ridAsIndex);
		ScanTableOffset(stgdbPointer, info, uncompressed, out uint tableAddressOffset, out uint nextTableOffset);
		var profile = new Profile {
			Vfptr = vfptr,
			Uncompressed = uncompressed,
			RidAsIndex = ridAsIndex,
			Schema = Utils.WithOffset(stgdbPointer, schemaOffset),
			TableCount = Utils.WithOffset(stgdbPointer, tableCountOffset),
			TableDefs = Utils.WithOffset(stgdbPointer, tableDefsOffset),
			MetadataAddress = Utils.WithOffset(stgdbPointer, metadataAddressOffset),
			MetadataSize = Utils.WithOffset(stgdbPointer, metadataSizeOffset),
			HeapAddress = Utils.WithOffset(stgdbPointer, heapAddressOffsets),
			HeapSize = Utils.WithOffset(stgdbPointer, heapSizeOffsets),
			TableAddress = Utils.WithOffset(stgdbPointer, tableAddressOffset),
			NextTableOffset = nextTableOffset
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
		// CMiniMdBase.m_Schema
	}

	static void ScanTableDefsOffsets(Pointer stgdbPointer, bool uncompressed, uint schemaOffset, out uint tableCountOffset, out uint tableDefsOffset) {
		const bool InMemory = false;

		int dummy = 0;
		var assemblyFlags = InMemory ? TestAssemblyFlags.InMemory : 0;
		if (uncompressed)
			assemblyFlags |= TestAssemblyFlags.Uncompressed;
		var assembly = TestAssemblyManager.GetAssembly(assemblyFlags);
		nuint module = assembly.ModuleHandle;
		Utils.Check((RuntimeDefinitions.Module*)module, assembly.Module.Assembly.GetName().Name);
		// Get native Module object

		nuint pSchema = Utils.ReadPointer(Utils.WithOffset(stgdbPointer, schemaOffset), module);
		nuint p = pSchema + (uint)sizeof(RuntimeDefinitions.CMiniMdSchema);
		uint m_TblCount = *(uint*)p;
		tableCountOffset = schemaOffset + (uint)(p - pSchema);
		Utils.Check(m_TblCount == RuntimeDefinitions.TBL_COUNT_V1 || m_TblCount == RuntimeDefinitions.TBL_COUNT_V2);
		// CMiniMdBase.m_TblCount

		if (RuntimeEnvironment.Version >= RuntimeVersion.Fx40)
			p += (uint)((nuint)(&((RuntimeDefinitions.CMiniMdBase_40*)&dummy)->m_TableDefs) - (nuint)(&((RuntimeDefinitions.CMiniMdBase_40*)&dummy)->m_TblCount));
		else
			p += (uint)((nuint)(&((RuntimeDefinitions.CMiniMdBase_20*)&dummy)->m_TableDefs) - (nuint)(&((RuntimeDefinitions.CMiniMdBase_20*)&dummy)->m_TblCount));
		tableDefsOffset = schemaOffset + (uint)(p - pSchema);
		var m_TableDefs = (RuntimeDefinitions.CMiniTableDef*)p;
		for (int i = 0; i < RuntimeDefinitions.TBL_COUNT; i++)
			Utils.Check(Memory.TryReadUInt32((nuint)m_TableDefs[i].m_pColDefs, out _));
		// CMiniMdBase.m_TableDefs
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

	static void ScanTableOffset(Pointer stgdbPointer, MiniMetadataInfo info, bool uncompressed, out uint tableAddressOffset, out uint nextTableOffset) {
		const bool InMemory = false;

		var assemblyFlags = InMemory ? TestAssemblyFlags.InMemory : 0;
		if (uncompressed)
			assemblyFlags |= TestAssemblyFlags.Uncompressed;
		var assembly = TestAssemblyManager.GetAssembly(assemblyFlags);
		nuint module = assembly.ModuleHandle;
		Utils.Check((RuntimeDefinitions.Module*)module, assembly.Module.Assembly.GetName().Name);
		// Get native Module object

		tableAddressOffset = 0;
		nextTableOffset = 0;
		nuint pStgdb = Utils.ReadUIntPtr(stgdbPointer, module);
		uint start = uncompressed ? (sizeof(nuint) == 4 ? 0x2A0u : 0x500) : (sizeof(nuint) == 4 ? 0x200u : 0x350);
		uint end = uncompressed ? (sizeof(nuint) == 4 ? 0x4A0u : 0x800) : (sizeof(nuint) == 4 ? 0x300u : 0x450);
		for (uint offset = start; offset < end; offset += 4) {
			nuint pFirst = pStgdb + offset;
			if (*(nuint*)pFirst != info.TableAddress[0])
				continue;

			uint start2 = 4;
			uint end2 = uncompressed ? 0x100u : 0x20;
			uint offset2 = start2;
			for (; offset2 < end2; offset2 += 4) {
				if (*(nuint*)(pFirst + offset2) != info.TableAddress[1])
					continue;
				if (*(nuint*)(pFirst + (2 * offset2)) != info.TableAddress[2])
					continue;
				break;
			}
			if (offset2 == end2)
				continue;

			tableAddressOffset = offset;
			nextTableOffset = offset2;
			break;
		}
		Utils.Check(tableAddressOffset != 0);
		Utils.Check(nextTableOffset != 0);
		// CMiniMd.m_Tables
	}

	public static MetadataInfo GetMetadataInfo(Module module) {
		if (module is null)
			throw new ArgumentNullException(nameof(module));

		Initialize();
		var profile = FindProfile(module);
		if (profile is null)
			return MetadataInfo.Empty;

		nuint moduleHandle = ReflectionHelpers.GetModuleHandle(module);
		var schema = GetSchema(profile, moduleHandle);
		var metadataInfo = new MetadataInfo {
			MetadataAddress = Utils.ReadUIntPtr(profile.MetadataAddress, moduleHandle),
			MetadataSize = Utils.ReadUInt32(profile.MetadataSize, moduleHandle),
			Schema = schema,
			TableStream = GetTableStream(profile, moduleHandle, schema),
			StringHeap = GetHeapInfo(profile, StringHeapIndex, moduleHandle),
			UserStringHeap = GetHeapInfo(profile, UserStringsHeapIndex, moduleHandle),
			GuidHeap = GetHeapInfo(profile, GuidHeapIndex, moduleHandle),
			BlobHeap = GetHeapInfo(profile, BlobHeapIndex, moduleHandle)
		};
		return metadataInfo;
	}

	static Profile? FindProfile(Module module) {
		nuint vfptr = MetadataImport.Create(module).Vfptr;
		foreach (var profile in profiles) {
			if (vfptr == profile.Vfptr)
				return profile;
		}
		Debug2.Assert(false);
		return null;
	}

	static MetadataSchema GetSchema(Profile profile, nuint moduleHandle) {
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
			RowCounts = rows,
			ExtraData = pSchema->m_ulExtra
		};
		return schema;
	}

	static MetadataTableInfo GetTableStream(Profile profile, nuint moduleHandle, MetadataSchema schema) {
		uint tableCount = Utils.ReadUInt32(profile.TableCount, moduleHandle);
		var rowSizes = GetRowSizes(profile, moduleHandle);
		var rowCounts = schema.RowCounts;
		uint tablesSize = 0;
		uint validTableCount = 0;
		for (int i = 0; i < (int)tableCount; i++) {
			if ((schema.ValidMask & (1ul << i)) == 0)
				continue;
			tablesSize += rowSizes[i] * rowCounts[i];
			validTableCount++;
		}
		uint headerSize = 0x18 + (validTableCount * 4);
		nuint pTable = Utils.ReadUIntPtr(profile.TableAddress, moduleHandle);
		nuint address = pTable - headerSize;
		uint size = headerSize + tablesSize;
		var tableInfo = new MetadataTableInfo {
			Address = address,
			Length = size,
			IsCompressed = !profile.Uncompressed,
			TableCount = tableCount,
			RowSizes = rowSizes
		};
		return tableInfo;
	}

	static uint[] GetRowSizes(Profile profile, nuint moduleHandle) {
		var tableDefs = (RuntimeDefinitions.CMiniTableDef*)Utils.ReadPointer(profile.TableDefs, moduleHandle);
		var rowSizes = new uint[RuntimeDefinitions.TBL_COUNT];
		for (int i = 0; i < RuntimeDefinitions.TBL_COUNT; i++)
			rowSizes[i] = tableDefs[i].m_cbRec;
		return rowSizes;
	}

	static MetadataHeapInfo GetHeapInfo(Profile profile, int index, nuint moduleHandle) {
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
		public uint[] RowCounts;
		public nuint[] TableAddress;

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
			TableAddress = new nuint[RuntimeDefinitions.TBL_COUNT];
			RowCounts = new uint[RuntimeDefinitions.TBL_COUNT];
			p = TableStreamAddress + 0x18;
			for (int i = 0; i < RuntimeDefinitions.TBL_COUNT; i++) {
				if ((ValidMask & (1ul << i)) == 0)
					continue;
				RowCounts[i] = *(uint*)p;
				p += 4;
			}
			TableAddress[0] = p;
		}

		public void CalculateTableAddress(nuint tableDefs, bool ridAsIndex) {
			nuint p = TableAddress[0];
			var tableDefs2 = (RuntimeDefinitions.CMiniTableDef*)tableDefs;
			for (int i = 0; i < RuntimeDefinitions.TBL_COUNT; i++) {
				TableAddress[i] = ridAsIndex ? p - tableDefs2[i].m_cbRec : p;
				p += RowCounts[i] * tableDefs2[i].m_cbRec;
			}
		}
	}
}
