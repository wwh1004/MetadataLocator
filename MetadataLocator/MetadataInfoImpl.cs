using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MetadataLocator.NativeSharp;
using Pointer = MetadataLocator.NativeSharp.Pointer;

namespace MetadataLocator;

static unsafe class MetadataInfoImpl {
	public static MetadataInfo GetMetadataInfo(Module module) {
		if (module is null)
			throw new ArgumentNullException(nameof(module));

		var metaDataTables = MetadataInterfaceHelper.GetIMetaDataTables(MetadataInterfaceHelper.GetMetadataImport(module));
		if (metaDataTables is null)
			throw new InvalidOperationException();
		var metadataInfo = new MetadataInfo {
			Module = module,
			MetaDataTables = metaDataTables
		};
		metadataInfo.TableStream = GetTableStream(metadataInfo);
		metadataInfo.StringHeap = GetStringHeap(metadataInfo);
		metadataInfo.UserStringHeap = GetUserStringHeap(metadataInfo);
		metadataInfo.GuidHeap = GetGuidHeap(metadataInfo);
		metadataInfo.BlobHeap = GetBlobHeap(metadataInfo);
		metadataInfo.PEInfo = DotNetPEInfoImpl.GetDotNetPEInfo(module);
		return metadataInfo;
	}

	static MetadataStreamInfo GetTableStream(MetadataInfo metadataInfo) {
		ThrowOnError(metadataInfo.MetaDataTables.GetNumTables(out uint tableCount));
		uint tablesSize = 0;
		uint validTableCount = 0;
		for (uint i = 0; i < tableCount; i++) {
			ThrowOnError(metadataInfo.MetaDataTables.GetTableInfo(i, out uint rowSize, out uint rowCount, out _, out _, out _));
			if (rowCount == 0)
				continue;
			tablesSize += rowSize * rowCount;
			validTableCount++;
		}
		uint headerSize = 0x18 + (validTableCount * 4);
		ThrowOnError(metadataInfo.MetaDataTables.GetRow(0, 1, out nuint address));
		return new MetadataStreamInfo {
			Address = address - headerSize,
			Length = AlignUp(headerSize + tablesSize, 4)
		};
	}

	static MetadataStreamInfo GetStringHeap(MetadataInfo metadataInfo) {
		ThrowOnError(metadataInfo.MetaDataTables.GetStringHeapSize(out uint streamSize));
		if (streamSize == 1)
			return null;
		// 表示流不存在，1只是用来占位
		int result = metadataInfo.MetaDataTables.GetString(0, out nuint pData);
		return result == 0 ? new MetadataStreamInfo {
			Address = pData,
			Length = AlignUp(streamSize, 4)
		} : null;
	}

	static MetadataStreamInfo GetUserStringHeap(MetadataInfo metadataInfo) {
		ThrowOnError(metadataInfo.MetaDataTables.GetUserStringHeapSize(out uint streamSize));
		if (streamSize == 1)
			return null;
		int result = metadataInfo.MetaDataTables.GetUserString(1, out uint dataSize, out nuint pData);
		// #US与#Blob堆传入ixXXX=0都会导致获取到的pData不是真实地址，所以获取第2个数据的地址
		return result == 0 ? new MetadataStreamInfo {
			Address = pData - GetCompressedUInt32Length(dataSize) - 1,
			Length = AlignUp(streamSize, 4)
		} : null;
	}

	static MetadataStreamInfo GetGuidHeap(MetadataInfo metadataInfo) {
		ThrowOnError(metadataInfo.MetaDataTables.GetGuidHeapSize(out uint streamSize));
		if (streamSize == 1)
			return null;
		int result = metadataInfo.MetaDataTables.GetGuid(1, out nuint pData);
		return result == 0 ? new MetadataStreamInfo {
			Address = pData,
			Length = AlignUp(streamSize, 4)
		} : null;
	}

	static MetadataStreamInfo GetBlobHeap(MetadataInfo metadataInfo) {
		ThrowOnError(metadataInfo.MetaDataTables.GetBlobHeapSize(out uint streamSize));
		if (streamSize == 1)
			return null;
		int result = metadataInfo.MetaDataTables.GetBlob(1, out uint dataSize, out nuint pData);
		return result == 0 ? new MetadataStreamInfo {
			Address = pData - GetCompressedUInt32Length(dataSize) - 1,
			Length = AlignUp(streamSize, 4)
		} : null;
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

	static void ThrowOnError(int result) {
		if (result != 0)
			throw new InvalidOperationException();
	}
}

static unsafe class DotNetPEInfoImpl {
	static FieldInfo moduleHandleField;
	static nuint[] testModuleHandles;
	static nuint[] testMemoryModuleHandles;
	static Pointer cor20HeaderAddressPointerTemplate;
	static Pointer metadataAddressPointerTemplate;
	static Pointer metadataSizePointerTemplate;

	static FieldInfo ModuleHandleField {
		get {
			if (moduleHandleField == null) {
				switch (Environment.Version.Major) {
				case 2:
					moduleHandleField = typeof(ModuleHandle).GetField("m_ptr", BindingFlags.NonPublic | BindingFlags.Instance);
					break;
				case 4:
					moduleHandleField = typeof(object).Module.GetType("System.Reflection.RuntimeModule").GetField("m_pData", BindingFlags.NonPublic | BindingFlags.Instance);
					break;
				default:
					throw new NotSupportedException();
				}
			}
			return moduleHandleField;
		}
	}

	static nuint[] TestModuleHandles {
		get {
			if (testModuleHandles == null) {
				var testModuleHandles = Enumerable.Range(0, 3).Select(t => GetModuleHandle(GenerateAssembly(false).ManifestModule)).ToArray();
				DotNetPEInfoImpl.testModuleHandles = new nuint[testModuleHandles.Length];
				for (int i = 0; i < testModuleHandles.Length; i++)
					DotNetPEInfoImpl.testModuleHandles[i] = testModuleHandles[i];
			}
			return testModuleHandles;
		}
	}

	static nuint[] TestMemoryModuleHandles {
		get {
			if (testMemoryModuleHandles == null) {
				var testModuleHandles = Enumerable.Range(0, 3).Select(t => GetModuleHandle(GenerateAssembly(true).ManifestModule)).ToArray();
				testMemoryModuleHandles = new nuint[testModuleHandles.Length];
				for (int i = 0; i < testModuleHandles.Length; i++)
					testMemoryModuleHandles[i] = testModuleHandles[i];
			}
			return testMemoryModuleHandles;
		}
	}

	static Pointer Cor20HeaderAddressPointerTemplate {
		get {
			if (cor20HeaderAddressPointerTemplate == null) {
				cor20HeaderAddressPointerTemplate = GetFirstValidTemplate(ScanCor20HeaderAddressPointerTemplates(), pCorHeader => CheckCor20HeaderAddressPointer(pCorHeader));
				if (cor20HeaderAddressPointerTemplate == null)
					throw new InvalidOperationException();
			}
			return cor20HeaderAddressPointerTemplate;
		}
	}

	static Pointer MetadataAddressPointerTemplate {
		get {
			if (metadataAddressPointerTemplate == null) {
				metadataAddressPointerTemplate = GetFirstValidTemplate(ScanMetadataAddressPointerTemplates(), pMetadata => CheckMetadataAddressPointer(pMetadata));
				if (metadataAddressPointerTemplate == null)
					throw new InvalidOperationException();
			}
			return metadataAddressPointerTemplate;
		}
	}

	static Pointer MetadataSizePointerTemplate {
		get {
			if (metadataSizePointerTemplate == null) {
				metadataSizePointerTemplate = new Pointer(MetadataAddressPointerTemplate);
				var offsets = metadataSizePointerTemplate.Offsets;
				offsets[offsets.Count - 1] += (uint)sizeof(nuint);
			}
			return metadataSizePointerTemplate;
		}
	}

	public static DotNetPEInfo GetDotNetPEInfo(Module module) {
		if (module is null)
			throw new ArgumentNullException(nameof(module));

		var peInfo = new DotNetPEInfo {
			IsValid = !IsNativeImage(module)
		};
		if (!peInfo.IsValid)
			return peInfo;
		nuint moduleHandle = GetModuleHandle(module);
		try {
			// 如果是#-表流，会出错，暂时不支持#-表流
			peInfo.Cor20HeaderAddress = ReadIntPtr(MakePointer(Cor20HeaderAddressPointerTemplate, moduleHandle));
			peInfo.MetadataAddress = ReadIntPtr(MakePointer(MetadataAddressPointerTemplate, moduleHandle));
			peInfo.MetadataSize = ReadUInt32(MakePointer(MetadataSizePointerTemplate, moduleHandle));
		}
		catch {
			peInfo.IsValid = false;
			return peInfo;
		}
		peInfo.ImageLayout = GetImageLayout(module);
		return peInfo;
	}

	static bool IsNativeImage(Module module) {
		try {
			string moduleName = Path.GetFileName(module.Assembly.Location);
			moduleName = Path.GetFileNameWithoutExtension(moduleName) + ".ni" + Path.GetExtension(moduleName);
			return NativeProcess.GetModule(moduleName) != 0;
		}
		catch {
			return false;
		}
	}

	static ImageLayout GetImageLayout(Module module) {
		string name = module.FullyQualifiedName;
		if (name.Length > 0 && name[0] == '<' && name[name.Length - 1] == '>')
			return ImageLayout.File;
		return ImageLayout.Memory;
	}

	static uint ReadUInt32(Pointer pointer) {
		if (!TryToAddress(pointer, out nuint address))
			return default;
		if (!TryReadUInt32(address, out uint value))
			return default;
		return value;
	}

	static nuint ReadIntPtr(Pointer pointer) {
		if (!TryToAddress(pointer, out nuint address))
			return default;
		if (!TryReadIntPtr(address, out nuint value))
			return default;
		return value;
	}

	static Assembly GenerateAssembly(bool isInMemory) {
		using var provider = CodeDomProvider.CreateProvider("cs");
		var options = new CompilerParameters {
			GenerateExecutable = false,
			OutputAssembly = Path.Combine(Path.GetTempPath(), $"___{Guid.NewGuid()}.dll")
		};
		var assembly = new CodeCompileUnit();
		var @namespace = new CodeNamespace("ns1");
		assembly.Namespaces.Add(@namespace);
		// write namespace
		var @class = new CodeTypeDeclaration("class1");
		@namespace.Types.Add(@class);
		// write class
		@class.Members.Add(new CodeMemberMethod() {
			Name = "method1"
		});
		// write method
		var results = provider.CompileAssemblyFromDom(options, assembly);
		var compiledAssembly = isInMemory ? Assembly.Load(File.ReadAllBytes(results.PathToAssembly)) : Assembly.LoadFile(results.PathToAssembly);
		return compiledAssembly;
	}

	static Pointer GetFirstValidTemplate(List<Pointer> templates, Predicate<nuint> checker) {
		foreach (var template in templates) {
			foreach (nuint moduleHandle in TestModuleHandles) {
				if (!TryToAddress(MakePointer(template, moduleHandle), out nuint address) || !TryReadIntPtr(address, out nuint value) || !checker(value))
					goto next;
			}
			foreach (nuint moduleHandle in TestMemoryModuleHandles) {
				if (!TryToAddress(MakePointer(template, moduleHandle), out nuint address) || !TryReadIntPtr(address, out nuint value) || !checker(value))
					goto next;
			}
			return template;
		next:
			continue;
		}
		return null;
	}

	static Pointer MakePointer(Pointer template, nuint moduleHandle) {
		return new Pointer(template) { BaseAddress = moduleHandle };
	}

	static List<Pointer> ScanCor20HeaderAddressPointerTemplates() {
		var m_fileOffsets = sizeof(nuint) == 4 ? new uint[] { 0x4, 0x8 } : new uint[] { 0x8, 0x10 };
		// Module.m_file
		var m_identityOffsets = sizeof(nuint) == 4 ? new uint[] { 0x8 } : new uint[] { 0x10 };
		// PEFile.m_openedILimage
		var unknownOffset1s = Enumerable.Range(0, (0x100 - 0x20) / 4).Select(t => 0x20 + ((uint)t * 4)).ToArray();
		// PEImage.????
		var m_pCorHeaderOffsets = sizeof(nuint) == 4 ? new uint[] { 0x14 } : new uint[] { 0x20 };
		// PEDecoder.m_pCorHeader
		var offsetMatrix = new uint[][] {
			m_fileOffsets,
			m_identityOffsets,
			unknownOffset1s,
			m_pCorHeaderOffsets
		};
		return ScanPointerTemplates(TestModuleHandles[0], offsetMatrix, pCorHeader => CheckCor20HeaderAddressPointer(pCorHeader));
	}

	static bool CheckCor20HeaderAddressPointer(nuint pCorHeader) {
		if (!TryReadUInt32(pCorHeader, out uint cb))
			return false;
		return cb == 0x48;
	}

	static List<Pointer> ScanMetadataAddressPointerTemplates() {
		var m_fileOffsets = sizeof(nuint) == 4 ? new uint[] { 0x4, 0x8 } : new uint[] { 0x8, 0x10 };
		// Module.m_file
		var unknownOffset1s = Enumerable.Range(0, (0x3C - 0x10) / 4).Select(t => 0x10 + ((uint)t * 4)).ToArray();
		// PEFile.????
		var unknownOffset2s = sizeof(nuint) == 4
			? Enumerable.Range(0, (0x39C - 0x350) / 4).Select(t => 0x350 + ((uint)t * 4)).ToArray()
			: Enumerable.Range(0, (0x5FC - 0x5B0) / 4).Select(t => 0x5B0 + ((uint)t * 4)).ToArray();
		// ????.????
		var offsetMatrix = new uint[][] {
			m_fileOffsets,
			unknownOffset1s,
			unknownOffset2s
		};
		return ScanPointerTemplates(TestModuleHandles[0], offsetMatrix, pMetadata => CheckMetadataAddressPointer(pMetadata));
	}

	static bool CheckMetadataAddressPointer(nuint pMetadata) {
		if (!TryReadUInt32(pMetadata, out uint signature))
			return false;
		return signature == 0x424A5342;
	}

	static List<Pointer> ScanPointerTemplates(nuint baseAddress, uint[][] offsetMatrix, Predicate<nuint> checker) {
		int level = 0;
		// 表示第几级偏移
		int[] offsetIndices = new int[offsetMatrix.Length];
		// 表示每一级偏移对应在offsetMatrix中的索引
		nuint[] values = new nuint[offsetMatrix.Length];
		// 表示每一级地址的值
		var pointers = new List<Pointer>();
		while (true) {
			bool result = TryReadIntPtr((level > 0 ? values[level - 1] : baseAddress) + offsetMatrix[level][offsetIndices[level]], out values[level]);
			// 读取当前偏移对应的值
			if (level == offsetMatrix.Length - 1) {
				// 是最后一级偏移
				if (result && checker(values[level]))
					pointers.Add(new Pointer(0, Enumerable.Range(0, offsetMatrix.Length).Select(t => offsetMatrix[t][offsetIndices[t]]).ToArray()));
				// 如果读取成功，说明是最后一级偏移，检测是否为有效指针，添加到列表
				offsetIndices[level]++;
				// 尝试当前级偏移数组的下一个偏移
			}
			else {
				// 不是最后一级偏移
				if (result)
					level++;
				else
					offsetIndices[level]++;
			}
			while (true) {
				// 回溯
				if (offsetIndices[level] == offsetMatrix[level].Length) {
					// 如果当前级偏移尝试完成了
					if (level > 0) {
						// 回溯到上一级，清空当前级数据
						offsetIndices[level] = 0;
						level -= 1;
						offsetIndices[level]++;
					}
					else {
						return pointers;
					}
					// 已经回溯到了level=0，说明扫描完成
				}
				else {
					break;
				}
			}
		}
	}

	static nuint GetModuleHandle(Module module) {
		switch (Environment.Version.Major) {
		case 2:
			return (nuint)(nint)ModuleHandleField.GetValue(module.ModuleHandle);
		case 4:
			return (nuint)(nint)ModuleHandleField.GetValue(module);
		default:
			throw new NotSupportedException();
		}
	}

	static bool TryToAddress(Pointer pointer, out nuint address) {
		return NativeProcess.TryToAddress(pointer, out address);
	}

	static bool TryReadUInt32(nuint address, out uint value) {
		return NativeProcess.TryReadUInt32(address, out value);
	}

	static bool TryReadIntPtr(nuint address, out nuint value) {
		return NativeProcess.TryReadIntPtr(address, out value);
	}
}
