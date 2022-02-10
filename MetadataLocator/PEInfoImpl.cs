using System;
using System.Diagnostics;
using System.IO;
using static MetadataLocator.RuntimeDefinitions;
using SR = System.Reflection;

namespace MetadataLocator;

static unsafe class PEInfoImpl {
	static Pointer filePathPointer = Pointer.Empty;
	static Pointer flatImageLayoutPointer = Pointer.Empty;
	static Pointer mappedImageLayoutPointer = Pointer.Empty;
	static Pointer loadedImageLayoutPointer = Pointer.Empty;
	static bool isInitialized;

	static void Initialize() {
		if (isInitialized)
			return;

		filePathPointer = ScanFilePathPointer();
		Debug2.Assert(!filePathPointer.IsEmpty);
		loadedImageLayoutPointer = ScanLoadedImageLayoutPointer(out bool isMappedLayoutExisting);
		Debug2.Assert(!loadedImageLayoutPointer.IsEmpty);
		if (isMappedLayoutExisting) {
			var t = new Pointer(loadedImageLayoutPointer);
			t.Offsets[t.Offsets.Count - 1] -= (uint)sizeof(nuint);
			mappedImageLayoutPointer = t;
			t = new Pointer(t);
			t.Offsets[t.Offsets.Count - 1] -= (uint)sizeof(nuint);
			flatImageLayoutPointer = t;
		}
		else {
			var t = new Pointer(loadedImageLayoutPointer);
			t.Offsets[t.Offsets.Count - 1] -= (uint)sizeof(nuint);
			flatImageLayoutPointer = t;
		}
		isInitialized = true;
	}

	static Pointer ScanFilePathPointer() {
		const bool InMemory = false;

		var assemblyFlags = InMemory ? TestAssemblyFlags.InMemory : 0;
		var assembly = TestAssemblyManager.GetAssembly(assemblyFlags);
		nuint module = assembly.ModuleHandle;
		Utils.Check((Module*)module, assembly.Module.Assembly.GetName().Name);
		// Get native Module object

		uint m_file_Offset;
		if (RuntimeEnvironment.Version >= RuntimeVersion.Fx453)
			m_file_Offset = (uint)((nuint)(&Module_453.Dummy->m_file) - (nuint)Module_453.Dummy);
		else
			m_file_Offset = (uint)((nuint)(&Module_20.Dummy->m_file) - (nuint)Module_20.Dummy);
		nuint m_file = *(nuint*)(module + m_file_Offset);
		Utils.Check((PEFile*)m_file);
		// Module.m_file

		uint m_openedILimage_Offset = (uint)((nuint)(&PEFile.Dummy->m_openedILimage) - (nuint)PEFile.Dummy);
		nuint m_openedILimage = *(nuint*)(m_file + m_openedILimage_Offset);
		Utils.Check((PEImage*)m_openedILimage, InMemory);
		// PEFile.m_openedILimage

		uint m_path_m_buffer_Offset = 0;
		if (RuntimeEnvironment.Version >= RuntimeVersion.Fx40)
			m_path_m_buffer_Offset = (uint)((nuint)(&PEImage_40.Dummy->m_path.m_buffer) - (nuint)PEImage_40.Dummy);
		else
			m_path_m_buffer_Offset = (uint)((nuint)(&PEImage_20.Dummy->m_path.m_buffer) - (nuint)PEImage_20.Dummy);
		nuint m_path_m_buffer = *(nuint*)(m_openedILimage + m_path_m_buffer_Offset);
		Utils.Check(Memory.TryReadUnicodeString(m_path_m_buffer, out var path) && File.Exists(path));
		// PEImage.m_path.m_buffer

		var pointer = new Pointer(new[] {
			m_file_Offset,
			m_openedILimage_Offset,
			m_path_m_buffer_Offset
		});
		Utils.Check(Utils.Verify(pointer, null, p => Memory.TryReadUnicodeString(p, out var path) && (path.Length == 0 || File.Exists(path))));
		pointer.Add(0);
		return pointer;
	}

	static Pointer ScanLoadedImageLayoutPointer(out bool isMappedLayoutExisting) {
		const bool InMemory = true;

		var assemblyFlags = InMemory ? TestAssemblyFlags.InMemory : 0;
		var assembly = TestAssemblyManager.GetAssembly(assemblyFlags);
		nuint module = assembly.ModuleHandle;
		Utils.Check((Module*)module, assembly.Module.Assembly.GetName().Name);
		// Get native Module object

		uint m_file_Offset;
		if (RuntimeEnvironment.Version >= RuntimeVersion.Fx453)
			m_file_Offset = (uint)((nuint)(&Module_453.Dummy->m_file) - (nuint)Module_453.Dummy);
		else
			m_file_Offset = (uint)((nuint)(&Module_20.Dummy->m_file) - (nuint)Module_20.Dummy);
		nuint m_file = *(nuint*)(module + m_file_Offset);
		Utils.Check((PEFile*)m_file);
		// Module.m_file

		uint m_openedILimage_Offset = (uint)((nuint)(&PEFile.Dummy->m_openedILimage) - (nuint)PEFile.Dummy);
		nuint m_openedILimage = *(nuint*)(m_file + m_openedILimage_Offset);
		Utils.Check((PEImage*)m_openedILimage, InMemory);
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
		isMappedLayoutExisting = false;
		uint m_pLayouts_Loaded_Offset = m_pMDImport_Offset - 4 - (uint)sizeof(nuint);
		uint m_pLayouts_Offset_Min = m_pLayouts_Loaded_Offset - (4 * (uint)sizeof(nuint));
		nuint actualModuleBase = ReflectionHelpers.GetNativeModuleHandle(assembly.Module);
		found = false;
		for (; m_pLayouts_Loaded_Offset >= m_pLayouts_Offset_Min; m_pLayouts_Loaded_Offset -= 4) {
			var m_pLayout = *(RuntimeDefinitions.PEImageLayout**)(m_openedILimage + m_pLayouts_Loaded_Offset);
			if (!Memory.TryReadUIntPtr((nuint)m_pLayout, out _))
				continue;
			if (!Memory.TryReadUIntPtr(m_pLayout->__vfptr, out _))
				continue;
			if (actualModuleBase != m_pLayout->__base.m_base)
				continue;
			Debug2.Assert(InMemory);
			var m_pLayout_prev1 = *(RuntimeDefinitions.PEImageLayout**)(m_openedILimage + m_pLayouts_Loaded_Offset - (uint)sizeof(nuint));
			var m_pLayout_prev2 = *(RuntimeDefinitions.PEImageLayout**)(m_openedILimage + m_pLayouts_Loaded_Offset - (2 * (uint)sizeof(nuint)));
			if (m_pLayout_prev2 == m_pLayout)
				isMappedLayoutExisting = true;
			else if (m_pLayout_prev1 == m_pLayout)
				isMappedLayoutExisting = false; // latest .NET, TODO: update comment when .NET 7.0 released
			found = true;
			break;
		}
		Utils.Check(found);
		nuint m_pLayouts_Loaded = *(nuint*)(m_openedILimage + m_pLayouts_Loaded_Offset);
		Utils.Check((RuntimeDefinitions.PEImageLayout*)m_pLayouts_Loaded, InMemory);
		// PEImage.m_pLayouts[IMAGE_LOADED]

		uint m_pCorHeader_Offset = (uint)((nuint)(&RuntimeDefinitions.PEImageLayout.Dummy->__base.m_pCorHeader) - (nuint)RuntimeDefinitions.PEImageLayout.Dummy);
		nuint m_pCorHeader = *(nuint*)(m_pLayouts_Loaded + m_pCorHeader_Offset);
		Utils.Check((IMAGE_COR20_HEADER*)m_pCorHeader);
		// PEImageLayout.m_pCorHeader

		var pointer = new Pointer(new[] {
			m_file_Offset,
			m_openedILimage_Offset,
			m_pLayouts_Loaded_Offset
		});
		Utils.Check(Utils.Verify(pointer, null, p => Memory.TryReadUIntPtr(p + (uint)sizeof(nuint), out nuint @base) && (ushort)@base == 0));
		Utils.Check(Utils.Verify(Utils.WithOffset(pointer, m_pCorHeader_Offset), null, p => Memory.TryReadUInt32(p, out uint cb) && cb == 0x48));
		return pointer;
	}

	public static PEInfo GetPEInfo(SR.Module module) {
		if (module is null)
			throw new ArgumentNullException(nameof(module));

		Initialize();
		nuint moduleHandle = ReflectionHelpers.GetModuleHandle(module);
		var peInfo = new PEInfo {
			FilePath = Utils.ReadUnicodeString(filePathPointer, moduleHandle),
			FlatLayout = GetImageLayout(Utils.ReadUIntPtr(flatImageLayoutPointer, moduleHandle)),
			MappedLayout = GetImageLayout(Utils.ReadUIntPtr(mappedImageLayoutPointer, moduleHandle)),
			LoadedLayout = GetImageLayout(Utils.ReadUIntPtr(loadedImageLayoutPointer, moduleHandle))
		};
		return peInfo;
	}

	static PEImageLayout GetImageLayout(nuint pImageLayout) {
		if (pImageLayout == 0)
			return PEImageLayout.Empty;

		var pLayout = (RuntimeDefinitions.PEImageLayout*)pImageLayout;
		var layout = new PEImageLayout {
			ImageBase = pLayout->__base.m_base,
			ImageSize = pLayout->__base.m_size,
			CorHeaderAddress = pLayout->__base.m_pCorHeader
		};
		return layout;
	}
}
