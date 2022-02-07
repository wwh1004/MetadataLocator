using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

static unsafe class DotNetPEInfoImpl {
	static Pointer Cor20HeaderAddressPointer = Pointer.Empty; // from IMAGE_LOADED
	static Pointer MetadataAddressPointer_RO = Pointer.Empty;
	static Pointer MetadataSizePointer_RO = Pointer.Empty;
	static nuint MDInternalRO_Vfptr;
	static Pointer MetadataAddressPointer_RW = Pointer.Empty;
	static Pointer MetadataSizePointer_RW = Pointer.Empty;
	static nuint MDInternalRW_Vfptr;
	static bool isInitialized;

	static void Initialize() {
		if (isInitialized)
			return;

		Cor20HeaderAddressPointer = ScanCor20HeaderAddressPointer();
		Debug2.Assert(!Cor20HeaderAddressPointer.IsEmpty);
		MetadataAddressPointer_RO = ScanMetadataAddressPointer(false, out MDInternalRO_Vfptr);
		Debug2.Assert(!MetadataAddressPointer_RO.IsEmpty);
		MetadataSizePointer_RO = new Pointer(MetadataAddressPointer_RO);
		MetadataSizePointer_RO.Offsets[MetadataSizePointer_RO.Offsets.Count - 1] += (uint)sizeof(nuint);
		MetadataAddressPointer_RW = ScanMetadataAddressPointer(true, out MDInternalRW_Vfptr);
		Debug2.Assert(!MetadataAddressPointer_RW.IsEmpty);
		MetadataSizePointer_RW = new Pointer(MetadataAddressPointer_RW);
		MetadataSizePointer_RW.Offsets[MetadataSizePointer_RW.Offsets.Count - 1] += (uint)sizeof(nuint);
		isInitialized = true;
	}

	public static DotNetPEInfo GetDotNetPEInfo(Module module) {
		if (module is null)
			throw new ArgumentNullException(nameof(module));

		Initialize();
		var peInfo = new DotNetPEInfo();
		nuint moduleHandle = ReflectionHelpers.GetModuleHandle(module);
		peInfo.Cor20HeaderAddress = ReadUIntPtr(Cor20HeaderAddressPointer, moduleHandle);
		nuint vfptr = MetadataImport.Create(module).Vfptr;
		if (vfptr == MDInternalRO_Vfptr) {
			peInfo.MetadataAddress = ReadUIntPtr(MetadataAddressPointer_RO, moduleHandle);
			peInfo.MetadataSize = ReadUInt32(MetadataSizePointer_RO, moduleHandle);
		}
		else if (vfptr == MDInternalRW_Vfptr) {
			peInfo.MetadataAddress = ReadUIntPtr(MetadataAddressPointer_RW, moduleHandle);
			peInfo.MetadataSize = ReadUInt32(MetadataSizePointer_RW, moduleHandle);
		}
		else {
			Debug2.Assert(false);
		}
		peInfo.ImageLayout = GetImageLayout(module);
		return peInfo;
	}

	static ImageLayout GetImageLayout(Module module) {
		string name = module.FullyQualifiedName;
		if (name.Length > 0 && name[0] == '<' && name[name.Length - 1] == '>')
			return ImageLayout.File;
		return ImageLayout.Memory;
	}

	static Pointer ScanCor20HeaderAddressPointer() {
		int dummy = 0;
		var assembly = TestAssemblyManager.GetAssembly(0);
		nuint module = assembly.ModuleHandle;
		// must be a loaded layout (load from file not memory)
		Check((RuntimeDefinitions.Module*)module);

		uint m_file_Offset;
		if (RuntimeEnvironment.Version >= RuntimeVersion.Fx453)
			m_file_Offset = (uint)((nuint)(&((RuntimeDefinitions.Module_453*)&dummy)->m_file) - (nuint)(&dummy));
		else
			m_file_Offset = (uint)((nuint)(&((RuntimeDefinitions.Module_20*)&dummy)->m_file) - (nuint)(&dummy));
		nuint m_file = *(nuint*)(module + m_file_Offset);
		Check((RuntimeDefinitions.PEFile*)m_file);
		// Module.m_file

		uint m_openedILimage_Offset = (uint)((nuint)(&((RuntimeDefinitions.PEFile*)&dummy)->m_openedILimage) - (nuint)(&dummy));
		nuint m_openedILimage = *(nuint*)(m_file + m_openedILimage_Offset);
		Check((RuntimeDefinitions.PEImage*)m_openedILimage);
		// PEFile.m_openedILimage

		nuint m_pMDImport = MetadataImport.Create(assembly.Module).This;
		uint m_pMDImport_Offset;
		bool found = false;
		for (m_pMDImport_Offset = 0x40; m_pMDImport_Offset < 0xD0; m_pMDImport_Offset += 4) {
			if (*(nuint*)(m_openedILimage + m_pMDImport_Offset) != m_pMDImport)
				continue;
			found = true;
			break;
		}
		Check(found);
		// PEFile.m_pMDImport (not use, just for locating previous member 'm_pLayouts')
		uint m_pLayouts_Loaded_Offset = m_pMDImport_Offset - 4 - (uint)sizeof(nuint);
		uint m_pLayouts_Loaded_Offset_Min = m_pLayouts_Loaded_Offset - (4 * (uint)sizeof(nuint));
		found = false;
		for (; m_pLayouts_Loaded_Offset >= m_pLayouts_Loaded_Offset_Min; m_pLayouts_Loaded_Offset -= 4) {
			var m_pLayout = *(RuntimeDefinitions.PEImageLayout**)(m_openedILimage + m_pLayouts_Loaded_Offset);
			if (!Memory.TryReadUIntPtr((nuint)m_pLayout, out _))
				continue;
			if (!Memory.TryReadUIntPtr(m_pLayout->__vfptr, out _))
				continue;
			nuint actualModuleBase = ReflectionHelpers.GetNativeModuleHandle(assembly.Module);
			if (actualModuleBase != m_pLayout->__base.m_base)
				continue;
			var m_pLayout2 = *(RuntimeDefinitions.PEImageLayout**)(m_openedILimage + m_pLayouts_Loaded_Offset - (uint)sizeof(nuint));
			Console.WriteLine($"{(uint)m_pLayout->__vfptr:X8} {(uint)m_pLayout2->__vfptr:X8}");
			found = true;
			break;
		}
		Check(found);
		nuint m_pLayouts_Loaded = *(nuint*)(m_openedILimage + m_pLayouts_Loaded_Offset);
		Check((RuntimeDefinitions.PEImageLayout*)m_pLayouts_Loaded);
		// PEImage.m_pLayouts[IMAGE_LOADED]

		uint m_pCorHeader_Offset = (uint)((nuint)(&((RuntimeDefinitions.PEImageLayout*)&dummy)->__base.m_pCorHeader) - (nuint)(&dummy));
		nuint m_pCorHeader = *(nuint*)(m_pLayouts_Loaded + m_pCorHeader_Offset);
		// PEImageLayout.m_pCorHeader

		{
			var pointer2 = new Pointer(new[] {
				m_file_Offset,
				m_openedILimage_Offset,
				m_pLayouts_Loaded_Offset,
				0u
			});
			var pointer3 = new Pointer(new[] {
				m_file_Offset,
				m_openedILimage_Offset,
				m_pLayouts_Loaded_Offset - (uint)sizeof(nuint),
				0u
			});
			Console.WriteLine($"{(uint)ReadUIntPtr(pointer2, assembly.ModuleHandle):X8} {(uint)ReadUIntPtr(pointer3, assembly.ModuleHandle):X8}");
			Console.WriteLine($"{(uint)ReadUIntPtr(pointer2, ReflectionHelpers.GetModuleHandle(typeof(MetadataInfo).Module)):X8} {(uint)ReadUIntPtr(pointer3, ReflectionHelpers.GetModuleHandle(typeof(MetadataInfo).Module)):X8}");
			// TODO: Fx20下从文件加载程序集，或者任意clr版本加载一个混合模式程序集，clr会加载两遍，第一遍是MappedImageLayout用来作为IMDInternalImport的元数据源，第二遍是LoadedImageLayout用于Marshal.GetHINSTANCE，Module.GetIL等，非常奇怪的特性
			// Workaround: 先获取一个MappedImageLayout，然后判断是否有值。如果有，用MappedImageLayout的值来进行后续处理，如获取m_pCorHeader
			// TODO: 提供一个api，枚举所有ImageLayout，然后返回对应的ImageBase，ImageSize，m_pCorHeader，在Dump时可以尝试所有的值，增加成功的概率
		}

		var pointer = new Pointer(new[] {
			m_file_Offset,
			m_openedILimage_Offset,
			m_pLayouts_Loaded_Offset,
			m_pCorHeader_Offset
		});
		Check(Verify(pointer, null, p => Memory.TryReadUInt32(p, out uint cb) && cb == 0x48));
		return pointer;
	}

	static Pointer ScanMetadataAddressPointer(bool uncompressed, out nuint vfptr) {
		int dummy = 0;
		var assembly = TestAssemblyManager.GetAssembly(uncompressed ? TestAssemblyFlags.Uncompressed : 0);
		nuint module = assembly.ModuleHandle;
		// must be a loaded layout (load from file not memory)
		Check((RuntimeDefinitions.Module*)module);

		uint m_file_Offset;
		if (RuntimeEnvironment.Version >= RuntimeVersion.Fx453)
			m_file_Offset = (uint)((nuint)(&((RuntimeDefinitions.Module_453*)&dummy)->m_file) - (nuint)(&dummy));
		else
			m_file_Offset = (uint)((nuint)(&((RuntimeDefinitions.Module_20*)&dummy)->m_file) - (nuint)(&dummy));
		nuint m_file = *(nuint*)(module + m_file_Offset);
		Check((RuntimeDefinitions.PEFile*)m_file);
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
		Check(found);
		// PEFile.m_pMDImport

		var m_pCorHeader = (RuntimeDefinitions.IMAGE_COR20_HEADER*)ReadUIntPtr(Cor20HeaderAddressPointer, assembly.ModuleHandle);
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
		}
		Check(m_pvMd_Offset != 0);

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
		Check(Verify(pointer, uncompressed, p => Memory.TryReadUInt32(p, out uint signature) && signature == 0x424A5342));
		return pointer;
	}

	static void Check(RuntimeDefinitions.Module* module) {
		Check(Memory.TryReadUIntPtr((nuint)module, out _));
		if (RuntimeEnvironment.Version >= RuntimeVersion.Fx453) {
			nuint m_pSimpleName = ((RuntimeDefinitions.Module_453*)module)->m_pSimpleName;
			bool b = Memory.TryReadUtf8String(m_pSimpleName, out var moduleName) && moduleName.All(t => char.IsLetterOrDigit(t) || t == '_' || t == '-');
			Check(b);
		}
	}

	static void Check(RuntimeDefinitions.PEFile* file) {
		Check(Memory.TryReadUIntPtr((nuint)file, out _));
	}

	static void Check(RuntimeDefinitions.PEImage* image) {
		Check(Memory.TryReadUIntPtr((nuint)image, out _));
		if (RuntimeEnvironment.Version >= RuntimeVersion.Fx40) {
			var m_Path = ((RuntimeDefinitions.PEImage_40*)image)->m_path;
			bool b = Memory.TryReadUnicodeString(m_Path.m_buffer, out var path) && File.Exists(path);
			Check(b);
		}
		else {
			var m_Path = ((RuntimeDefinitions.PEImage_20*)image)->m_path;
			bool b = Memory.TryReadUnicodeString(m_Path.m_buffer, out var path) && File.Exists(path);
			Check(b);
		}
	}

	static void Check(RuntimeDefinitions.PEImageLayout* layout) {
		Check(Memory.TryReadUIntPtr((nuint)layout, out _));
		Check((ushort)layout->__base.m_base == 0);
		Check(layout->__base.m_pCorHeader - layout->__base.m_base == 0x2008);
	}

	static void Check(RuntimeDefinitions.IMAGE_COR20_HEADER* corHeader) {
		Check(Memory.TryReadUIntPtr((nuint)corHeader, out _));
		Check(corHeader->cb == 0x48);
		Check(corHeader->MajorRuntimeVersion == 2);
		Check(corHeader->MinorRuntimeVersion == 2);
	}

	static void Check(bool condition) {
		if (!condition) {
			Debug2.Assert(false, "Contains error in RuntimeDefinitions");
			throw new InvalidOperationException("Contains error in RuntimeDefinitions");
		}
	}

	static bool Verify(Pointer pointer, bool? testUncompressed, Predicate<nuint> checker) {
		for (int i = 0; i < 5; i++) {
			for (TestAssemblyFlags inMemory = 0; inMemory <= TestAssemblyFlags.InMemory; inMemory += (int)TestAssemblyFlags.InMemory) {
				var uncompressed = testUncompressed == true ? TestAssemblyFlags.Uncompressed : 0;
				var uncompressedEnd = testUncompressed == false ? 0 : TestAssemblyFlags.Uncompressed;
				for (; uncompressed <= uncompressedEnd; uncompressed += (int)TestAssemblyFlags.Uncompressed) {
					var assembly = TestAssemblyManager.GetAssembly((TestAssemblyFlags)i | inMemory | uncompressed);
					nuint value = ReadUIntPtr(pointer, assembly.ModuleHandle);
					if (value == 0 || !checker(value))
						return false;
				}
			}
		}
		return true;
	}

	static uint ReadUInt32(Pointer pointer, nuint baseAddress) {
		pointer = MakePointer(pointer, baseAddress);
		if (!Memory.TryToAddress(pointer, out nuint address))
			return default;
		if (!Memory.TryReadUInt32(address, out uint value))
			return default;
		return value;
	}

	static nuint ReadUIntPtr(Pointer pointer, nuint baseAddress) {
		pointer = MakePointer(pointer, baseAddress);
		if (!Memory.TryToAddress(pointer, out nuint address))
			return default;
		if (!Memory.TryReadUIntPtr(address, out nuint value))
			return default;
		return value;
	}

	static Pointer MakePointer(Pointer template, nuint baseAddress) {
		Debug2.Assert(!template.IsEmpty);
		return new Pointer(template) { BaseAddress = baseAddress };
	}
}
