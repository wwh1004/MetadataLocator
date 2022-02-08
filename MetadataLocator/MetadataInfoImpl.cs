using System;
using System.Diagnostics;
using System.Reflection;

namespace MetadataLocator;

static unsafe class MetadataInfoImpl {
	static Pointer metadataAddressPointer_RO = Pointer.Empty;
	static Pointer metadataSizePointer_RO = Pointer.Empty;
	static nuint metadataInterfaceVfptr_RO;
	static Pointer metadataAddressPointer_RW = Pointer.Empty;
	static Pointer metadataSizePointer_RW = Pointer.Empty;
	static nuint metadataInterfaceVfptr_RW;
	static bool isInitialized;

	static void Initialize() {
		if (isInitialized)
			return;

		metadataAddressPointer_RO = ScanMetadataAddressPointer(false, out metadataInterfaceVfptr_RO);
		Debug2.Assert(!metadataAddressPointer_RO.IsEmpty);
		metadataSizePointer_RO = new Pointer(metadataAddressPointer_RO);
		metadataSizePointer_RO.Offsets[metadataSizePointer_RO.Offsets.Count - 1] += (uint)sizeof(nuint);
		metadataAddressPointer_RW = ScanMetadataAddressPointer(true, out metadataInterfaceVfptr_RW);
		Debug2.Assert(!metadataAddressPointer_RW.IsEmpty);
		metadataSizePointer_RW = new Pointer(metadataAddressPointer_RW);
		metadataSizePointer_RW.Offsets[metadataSizePointer_RW.Offsets.Count - 1] += (uint)sizeof(nuint);
		isInitialized = true;
		// TODO: Fx20下从文件加载程序集，或者任意clr版本加载一个混合模式程序集，clr会加载两遍，第一遍是MappedImageLayout用来作为IMDInternalImport的元数据源，第二遍是LoadedImageLayout用于Marshal.GetHINSTANCE，Module.GetIL等，非常奇怪的特性
		// Workaround: 先获取一个MappedImageLayout，然后判断是否有值。如果有，用MappedImageLayout的值来进行后续处理，如获取m_pCorHeader
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

		var m_pCorHeader = (RuntimeDefinitions.IMAGE_COR20_HEADER*)Utils.ReadUIntPtr(/*Cor20HeaderAddressPointer*/ null, assembly.ModuleHandle);
		nuint m_pvMd = ReflectionHelpers.GetNativeModuleHandle(assembly.Module) + m_pCorHeader->MetaData.VirtualAddress;
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

		//nuint code = metadataImport.GetFunction(MetadataImportFunction.GetVersionString);
		//var constants = new List<ushort>();
		//while (*(byte*)code is not 0xC2 and not 0xC3) {
		//	var ldasm = new Ldasm();
		//	uint size = ldasm.ldasm(code, sizeof(nuint) == 8);
		//	if (ldasm.TryGetDisplacement(code, out uint displacement) && displacement < ushort.MaxValue)
		//		constants.Add((ushort)displacement);
		//	if (ldasm.TryGetImmediate(code, out ulong immediate) && immediate < ushort.MaxValue)
		//		constants.Add((ushort)immediate);
		//	if ((ldasm.flags & Ldasm.F_RELATIVE) != 0)
		//		break;
		//	// collect constants until first jmp
		//	code += size;
		//}
		// alternative method

		var pointer = new Pointer(new[] {
			m_file_Offset,
			m_pMDImport_Offset
		});
		if (m_pStgdb_Offset != 0)
			pointer.Add(m_pStgdb_Offset);
		pointer.Add(m_pvMd_Offset);
		Utils.Check(Utils.Verify(pointer, uncompressed, p => Memory.TryReadUInt32(p, out uint signature) && signature == 0x424A5342));
		return pointer;
	}

	public static MetadataInfo GetMetadataInfo(Module module) {
		if (module is null)
			throw new ArgumentNullException(nameof(module));

		_ = RuntimeEnvironment.Version;
		nuint moduleHandle = ReflectionHelpers.GetModuleHandle(module);
		nuint vfptr = MetadataImport.Create(module).Vfptr;
		if (vfptr == metadataInterfaceVfptr_RO) {
			Utils.ReadUIntPtr(metadataAddressPointer_RO, moduleHandle);
			Utils.ReadUInt32(metadataSizePointer_RO, moduleHandle);
		}
		else if (vfptr == metadataInterfaceVfptr_RW) {
			Utils.ReadUIntPtr(metadataAddressPointer_RW, moduleHandle);
			Utils.ReadUInt32(metadataSizePointer_RW, moduleHandle);
		}
		else {
			Debug2.Assert(false);
		}
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
