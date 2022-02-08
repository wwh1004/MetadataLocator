using System;
using System.Diagnostics;
using System.Reflection;

namespace MetadataLocator;

static unsafe class DotNetPEInfoImpl {
	static Pointer Cor20HeaderAddressPointer = Pointer.Empty; // from IMAGE_LOADED
	static bool isInitialized;

	static void Initialize() {
		if (isInitialized)
			return;

		Cor20HeaderAddressPointer = ScanCor20HeaderAddressPointer();
		Debug2.Assert(!Cor20HeaderAddressPointer.IsEmpty);
		isInitialized = true;
	}

	public static DotNetPEInfo GetDotNetPEInfo(Module module) {
		if (module is null)
			throw new ArgumentNullException(nameof(module));

		Initialize();
		var peInfo = new DotNetPEInfo();
		nuint moduleHandle = ReflectionHelpers.GetModuleHandle(module);
		return peInfo;
	}

	static Pointer ScanCor20HeaderAddressPointer() {
		int dummy = 0;
		var assembly = TestAssemblyManager.GetAssembly(0);
		nuint module = assembly.ModuleHandle;
		// must be a loaded layout (load from file not memory)
		Utils.Check((RuntimeDefinitions.Module*)module);

		uint m_file_Offset;
		if (RuntimeEnvironment.Version >= RuntimeVersion.Fx453)
			m_file_Offset = (uint)((nuint)(&((RuntimeDefinitions.Module_453*)&dummy)->m_file) - (nuint)(&dummy));
		else
			m_file_Offset = (uint)((nuint)(&((RuntimeDefinitions.Module_20*)&dummy)->m_file) - (nuint)(&dummy));
		nuint m_file = *(nuint*)(module + m_file_Offset);
		Utils.Check((RuntimeDefinitions.PEFile*)m_file);
		// Module.m_file

		uint m_openedILimage_Offset = (uint)((nuint)(&((RuntimeDefinitions.PEFile*)&dummy)->m_openedILimage) - (nuint)(&dummy));
		nuint m_openedILimage = *(nuint*)(m_file + m_openedILimage_Offset);
		Utils.Check((RuntimeDefinitions.PEImage*)m_openedILimage);
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
		Utils.Check(found);
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
		Utils.Check(found);
		nuint m_pLayouts_Loaded = *(nuint*)(m_openedILimage + m_pLayouts_Loaded_Offset);
		Utils.Check((RuntimeDefinitions.PEImageLayout*)m_pLayouts_Loaded);
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
			Console.WriteLine($"{(uint)Utils.ReadUIntPtr(pointer2, assembly.ModuleHandle):X8} {(uint)Utils.ReadUIntPtr(pointer3, assembly.ModuleHandle):X8}");
			Console.WriteLine($"{(uint)Utils.ReadUIntPtr(pointer2, ReflectionHelpers.GetModuleHandle(typeof(MetadataInfo).Module)):X8} {(uint)Utils.ReadUIntPtr(pointer3, ReflectionHelpers.GetModuleHandle(typeof(MetadataInfo).Module)):X8}");
			// TODO: Fx20下从文件加载程序集，或者任意clr版本加载一个混合模式程序集，clr会加载两遍，第一遍是MappedImageLayout用来作为IMDInternalImport的元数据源，第二遍是LoadedImageLayout用于Marshal.GetHINSTANCE，Module.GetIL等，非常奇怪的特性
			// Workaround: 先获取一个MappedImageLayout，然后判断是否有值。如果有，用MappedImageLayout的值来进行后续处理，如获取m_pCorHeader
			// TODO: 提供一个api，枚举所有ImageLayout，然后返回对应的ImageBase，ImageSize，m_pCorHeader，在Dump时可以尝试所有的值，增加成功的概率
			// https://github.com/dotnet/coreclr/commit/af4ec7c89d0192ad14392da04e8c097da8ec9e48#diff-dd1e605d2e73125b21c2617d94bb41def043971508334410a1d62675cf768b6dL332
			// https://github.com/dotnet/runtime/commit/35e4e97867db6bb2cc1c9f1e91c80dd80759e259#diff-42902be0f805f8e1cce666fd0dfb892ffbac53d3c5897beaa86d965df22ef1dbL287
		}

		var pointer = new Pointer(new[] {
			m_file_Offset,
			m_openedILimage_Offset,
			m_pLayouts_Loaded_Offset,
			m_pCorHeader_Offset
		});
		Utils.Check(Utils.Verify(pointer, null, p => Memory.TryReadUInt32(p, out uint cb) && cb == 0x48));
		return pointer;
	}


}
