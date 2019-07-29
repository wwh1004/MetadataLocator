using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NativeSharp;
using Pointer = NativeSharp.Pointer;

namespace MetadataLocator {
	internal static unsafe class MetadataInfoImpl {
		public static MetadataInfo GetMetadataInfo(Module module) {
			if (module is null)
				throw new ArgumentNullException(nameof(module));

			IMetaDataTables metaDataTables;
			MetadataInfo metadataInfo;

			metaDataTables = MetadataInterfaceHelper.GetIMetaDataTables(MetadataInterfaceHelper.GetMetadataImport(module));
			if (metaDataTables is null)
				throw new InvalidOperationException();
			metadataInfo = new MetadataInfo {
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

		private static MetadataStreamInfo GetTableStream(MetadataInfo metadataInfo) {
			uint tableCount;
			uint tablesSize;
			uint validTableCount;
			uint headerSize;
			void* address;

			ThrowOnError(metadataInfo.MetaDataTables.GetNumTables(out tableCount));
			tablesSize = 0;
			validTableCount = 0;
			for (uint i = 0; i < tableCount; i++) {
				uint rowSize;
				uint rowCount;

				ThrowOnError(metadataInfo.MetaDataTables.GetTableInfo(i, out rowSize, out rowCount, out _, out _, out _));
				if (rowCount == 0)
					continue;
				tablesSize += rowSize * rowCount;
				validTableCount++;
			}
			headerSize = 0x18 + validTableCount * 4;
			ThrowOnError(metadataInfo.MetaDataTables.GetRow(0, 1, out address));
			return new MetadataStreamInfo {
				Address = (byte*)address - headerSize,
				Length = AlignUp(headerSize + tablesSize, 4)
			};
		}

		private static MetadataStreamInfo GetStringHeap(MetadataInfo metadataInfo) {
			int result;
			uint streamSize;
			void* pData;

			ThrowOnError(metadataInfo.MetaDataTables.GetStringHeapSize(out streamSize));
			if (streamSize == 1)
				// 表示流不存在，1只是用来占位
				return null;
			result = metadataInfo.MetaDataTables.GetString(0, out pData);
			return result == 0 ? new MetadataStreamInfo {
				Address = pData,
				Length = AlignUp(streamSize, 4)
			} : null;
		}

		private static MetadataStreamInfo GetUserStringHeap(MetadataInfo metadataInfo) {
			int result;
			uint streamSize;
			uint dataSize;
			void* pData;

			ThrowOnError(metadataInfo.MetaDataTables.GetUserStringHeapSize(out streamSize));
			if (streamSize == 1)
				return null;
			result = metadataInfo.MetaDataTables.GetUserString(1, out dataSize, out pData);
			// #US与#Blob堆传入ixXXX=0都会导致获取到的pData不是真实地址，所以获取第2个数据的地址
			return result == 0 ? new MetadataStreamInfo {
				Address = (byte*)pData - GetCompressedUInt32Length(dataSize) - 1,
				Length = AlignUp(streamSize, 4)
			} : null;
		}

		private static MetadataStreamInfo GetGuidHeap(MetadataInfo metadataInfo) {
			int result;
			uint streamSize;
			void* pData;

			ThrowOnError(metadataInfo.MetaDataTables.GetGuidHeapSize(out streamSize));
			if (streamSize == 1)
				return null;
			result = metadataInfo.MetaDataTables.GetGuid(1, out pData);
			return result == 0 ? new MetadataStreamInfo {
				Address = pData,
				Length = AlignUp(streamSize, 4)
			} : null;
		}

		private static MetadataStreamInfo GetBlobHeap(MetadataInfo metadataInfo) {
			int result;
			uint streamSize;
			uint dataSize;
			void* pData;

			ThrowOnError(metadataInfo.MetaDataTables.GetBlobHeapSize(out streamSize));
			if (streamSize == 1)
				return null;
			result = metadataInfo.MetaDataTables.GetBlob(1, out dataSize, out pData);
			return result == 0 ? new MetadataStreamInfo {
				Address = (byte*)pData - GetCompressedUInt32Length(dataSize) - 1,
				Length = AlignUp(streamSize, 4)
			} : null;
		}

		private static uint AlignUp(uint value, uint alignment) {
			return (value + alignment - 1) & ~(alignment - 1);
		}

		private static byte GetCompressedUInt32Length(uint value) {
			if (value < 0x80)
				return 1;
			else if (value < 0x4000)
				return 2;
			else
				return 4;
		}

		private static void ThrowOnError(int result) {
			if (result != 0)
				throw new InvalidOperationException();
		}
	}

	internal static unsafe class DotNetPEInfoImpl {
		private static FieldInfo _moduleHandleField;
		private static void*[] _testModuleHandles;
		private static void*[] _testMemoryModuleHandles;
		private static Pointer _cor20HeaderAddressPointerTemplate;
		private static Pointer _metadataAddressPointerTemplate;
		private static Pointer _metadataSizePointerTemplate;

		private static FieldInfo ModuleHandleField {
			get {
				if (_moduleHandleField == null)
					switch (Environment.Version.Major) {
					case 2:
						_moduleHandleField = typeof(ModuleHandle).GetField("m_ptr", BindingFlags.NonPublic | BindingFlags.Instance);
						break;
					case 4:
						_moduleHandleField = typeof(object).Module.GetType("System.Reflection.RuntimeModule").GetField("m_pData", BindingFlags.NonPublic | BindingFlags.Instance);
						break;
					default:
						throw new NotSupportedException();
					}
				return _moduleHandleField;
			}
		}

		private static void*[] TestModuleHandles {
			get {
				if (_testModuleHandles == null) {
					IntPtr[] testModuleHandles;

					testModuleHandles = Enumerable.Range(0, 5).Select(t => (IntPtr)GetModuleHandle(GenerateAssembly(false).ManifestModule)).ToArray();
					_testModuleHandles = new void*[testModuleHandles.Length];
					for (int i = 0; i < testModuleHandles.Length; i++)
						_testModuleHandles[i] = (void*)testModuleHandles[i];
				}
				return _testModuleHandles;
			}
		}

		private static void*[] TestMemoryModuleHandles {
			get {
				if (_testMemoryModuleHandles == null) {
					IntPtr[] testModuleHandles;

					testModuleHandles = Enumerable.Range(0, 3).Select(t => (IntPtr)GetModuleHandle(GenerateAssembly(true).ManifestModule)).ToArray();
					_testMemoryModuleHandles = new void*[testModuleHandles.Length];
					for (int i = 0; i < testModuleHandles.Length; i++)
						_testMemoryModuleHandles[i] = (void*)testModuleHandles[i];
				}
				return _testMemoryModuleHandles;
			}
		}

		private static Pointer Cor20HeaderAddressPointerTemplate {
			get {
				if (_cor20HeaderAddressPointerTemplate == null) {
					_cor20HeaderAddressPointerTemplate = GetFirstValidTemplate(ScanCor20HeaderAddressPointerTemplates(), pCorHeader => CheckCor20HeaderAddressPointer((void*)pCorHeader));
					if (_cor20HeaderAddressPointerTemplate == null)
						throw new InvalidOperationException();
				}
				return _cor20HeaderAddressPointerTemplate;
			}
		}

		private static Pointer MetadataAddressPointerTemplate {
			get {
				if (_metadataAddressPointerTemplate == null) {
					_metadataAddressPointerTemplate = GetFirstValidTemplate(ScanMetadataAddressPointerTemplates(), pMetadata => CheckMetadataAddressPointer((void*)pMetadata));
					if (_metadataAddressPointerTemplate == null)
						throw new InvalidOperationException();
				}
				return _metadataAddressPointerTemplate;
			}
		}

		private static Pointer MetadataSizePointerTemplate {
			get {
				if (_metadataSizePointerTemplate == null) {
					IList<uint> offsets;

					_metadataSizePointerTemplate = new Pointer(MetadataAddressPointerTemplate);
					offsets = _metadataSizePointerTemplate.Offsets;
					offsets[offsets.Count - 1] += (uint)IntPtr.Size;
				}
				return _metadataSizePointerTemplate;
			}
		}

		public static DotNetPEInfo GetDotNetPEInfo(Module module) {
			if (module is null)
				throw new ArgumentNullException(nameof(module));

			DotNetPEInfo peInfo;
			void* moduleHandle;
			void* pCor20Header;
			void* pMetadata;
			uint metadataSize;

			peInfo = new DotNetPEInfo {
				IsValid = !IsNativeImage(module)
			};
			if (!peInfo.IsValid)
				return peInfo;
			moduleHandle = GetModuleHandle(module);
			try {
				// 如果是#-表流，会出错，暂时不支持#-表流
				pCor20Header = (void*)ReadIntPtr(MakePointer(Cor20HeaderAddressPointerTemplate, moduleHandle));
				pMetadata = (void*)ReadIntPtr(MakePointer(MetadataAddressPointerTemplate, moduleHandle));
				metadataSize = ReadUInt32(MakePointer(MetadataSizePointerTemplate, moduleHandle));
			}
			catch {
				peInfo.IsValid = false;
				return peInfo;
			}
			peInfo.Cor20HeaderAddress = pCor20Header;
			peInfo.MetadataAddress = pMetadata;
			peInfo.MetadataSize = metadataSize;
			peInfo.ImageLayout = GetImageLayout(module);
			return peInfo;
		}

		private static bool IsNativeImage(Module module) {
			try {
				string moduleName;

				moduleName = Path.GetFileName(module.Assembly.Location);
				moduleName = Path.GetFileNameWithoutExtension(moduleName) + ".ni" + Path.GetExtension(moduleName);
				return NativeProcess.CurrentProcess.GetModule(moduleName) != null;
			}
			catch {
				return false;
			}
		}

		private static ImageLayout GetImageLayout(Module module) {
			string name;

			name = module.FullyQualifiedName;
			if (name.Length > 0 && name[0] == '<' && name[name.Length - 1] == '>')
				return ImageLayout.File;
			return ImageLayout.Memory;
		}

		private static uint ReadUInt32(Pointer pointer) {
			void* address;
			uint value;

			if (!TryToAddress(pointer, out address))
				return default;
			if (!TryReadUInt32(address, out value))
				return default;
			return value;
		}

		private static IntPtr ReadIntPtr(Pointer pointer) {
			void* address;
			IntPtr value;

			if (!TryToAddress(pointer, out address))
				return default;
			if (!TryReadIntPtr(address, out value))
				return default;
			return value;
		}

		private static Assembly GenerateAssembly(bool isInMemory) {
			using (CodeDomProvider provider = CodeDomProvider.CreateProvider("cs")) {
				CompilerParameters options;
				CodeCompileUnit assembly;
				CodeNamespace @namespace;
				CodeTypeDeclaration @class;
				CompilerResults results;
				Assembly compiledAssembly;

				options = new CompilerParameters {
					GenerateExecutable = false,
					OutputAssembly = Path.Combine(Path.GetTempPath(), $"___{Guid.NewGuid()}.dll")
				};
				assembly = new CodeCompileUnit();
				@namespace = new CodeNamespace("ns1");
				assembly.Namespaces.Add(@namespace);
				// write namespace
				@class = new CodeTypeDeclaration("class1");
				@namespace.Types.Add(@class);
				// write class
				@class.Members.Add(new CodeMemberMethod() {
					Name = "method1"
				});
				// write method
				results = provider.CompileAssemblyFromDom(options, assembly);
				compiledAssembly = isInMemory ? Assembly.Load(File.ReadAllBytes(results.PathToAssembly)) : Assembly.LoadFile(results.PathToAssembly);
				return compiledAssembly;
			}
		}

		private static Pointer GetFirstValidTemplate(List<Pointer> templates, Predicate<IntPtr> checker) {
			foreach (Pointer template in templates) {
				foreach (void* moduleHandle in TestModuleHandles) {
					void* address;
					IntPtr value;

					if (!TryToAddress(MakePointer(template, moduleHandle), out address) || !TryReadIntPtr(address, out value) || !checker(value))
						goto next;
				}
				foreach (void* moduleHandle in TestMemoryModuleHandles) {
					void* address;
					IntPtr value;

					if (!TryToAddress(MakePointer(template, moduleHandle), out address) || !TryReadIntPtr(address, out value) || !checker(value))
						goto next;
				}
				return template;
			next:
				continue;
			}
			return null;
		}

		private static Pointer MakePointer(Pointer template, void* moduleHandle) {
			Pointer pointer;

			pointer = new Pointer(template) {
				BaseAddress = moduleHandle
			};
			return pointer;
		}

		private static List<Pointer> ScanCor20HeaderAddressPointerTemplates() {
			uint[] m_fileOffsets;
			uint[] m_identityOffsets;
			uint[] unknownOffset1s;
			uint[] m_pCorHeaderOffsets;
			uint[][] offsetMatrix;

			m_fileOffsets = IntPtr.Size == 4 ? new uint[] { 0x4, 0x8 } : new uint[] { 0x8, 0x10 };
			// Module.m_file
			m_identityOffsets = IntPtr.Size == 4 ? new uint[] { 0x8 } : new uint[] { 0x10 };
			// PEFile.m_openedILimage
			unknownOffset1s = Enumerable.Range(0, (0x80 - 0x20) / 4).Select(t => 0x20 + ((uint)t * 4)).ToArray();
			// PEImage.????
			m_pCorHeaderOffsets = IntPtr.Size == 4 ? new uint[] { 0x14 } : new uint[] { 0x20 };
			// PEDecoder.m_pCorHeader
			offsetMatrix = new uint[][] {
				m_fileOffsets,
				m_identityOffsets,
				unknownOffset1s,
				m_pCorHeaderOffsets
			};
			return ScanPointerTemplates(TestModuleHandles[0], offsetMatrix, pCorHeader => CheckCor20HeaderAddressPointer((void*)pCorHeader));
		}

		private static bool CheckCor20HeaderAddressPointer(void* pCorHeader) {
			uint cb;

			if (!TryReadUInt32(pCorHeader, out cb))
				return false;
			return cb == 0x48;
		}

		private static List<Pointer> ScanMetadataAddressPointerTemplates() {
			uint[] m_fileOffsets;
			uint[] unknownOffset1s;
			uint[] unknownOffset2s;
			uint[][] offsetMatrix;

			m_fileOffsets = IntPtr.Size == 4 ? new uint[] { 0x4, 0x8 } : new uint[] { 0x8, 0x10 };
			// Module.m_file
			unknownOffset1s = Enumerable.Range(0, (0x3C - 0x10) / 4).Select(t => 0x10 + ((uint)t * 4)).ToArray();
			// PEFile.????
			unknownOffset2s = IntPtr.Size == 4
				? Enumerable.Range(0, (0x39C - 0x350) / 4).Select(t => 0x350 + ((uint)t * 4)).ToArray()
				: Enumerable.Range(0, (0x5FC - 0x5B0) / 4).Select(t => 0x5B0 + ((uint)t * 4)).ToArray();
			// ????.????
			offsetMatrix = new uint[][] {
				m_fileOffsets,
				unknownOffset1s,
				unknownOffset2s
			};
			return ScanPointerTemplates(TestModuleHandles[0], offsetMatrix, pMetadata => CheckMetadataAddressPointer((void*)pMetadata));
		}

		private static bool CheckMetadataAddressPointer(void* pMetadata) {
			uint signature;

			if (!TryReadUInt32(pMetadata, out signature))
				return false;
			return signature == 0x424A5342;
		}

		private static List<Pointer> ScanPointerTemplates(void* baseAddress, uint[][] offsetMatrix, Predicate<IntPtr> checker) {
			int level;
			int[] offsetIndices;
			void*[] values;
			List<Pointer> pointers;

			level = 0;
			// 表示第几级偏移
			offsetIndices = new int[offsetMatrix.Length];
			// 表示每一级偏移对应在offsetMatrix中的索引
			values = new void*[offsetMatrix.Length];
			// 表示每一级地址的值
			pointers = new List<Pointer>();
			while (true) {
				bool result;

				result = TryReadIntPtr((byte*)(level > 0 ? values[level - 1] : baseAddress) + offsetMatrix[level][offsetIndices[level]], out IntPtr temp);
				values[level] = (void*)temp;
				// 读取当前偏移对应的值
				if (level == offsetMatrix.Length - 1) {
					// 是最后一级偏移
					if (result && checker((IntPtr)values[level]))
						// 如果读取成功，说明是最后一级偏移，检测是否为有效指针，添加到列表
						pointers.Add(new Pointer(null, Enumerable.Range(0, offsetMatrix.Length).Select(t => offsetMatrix[t][offsetIndices[t]]).ToArray()));
					offsetIndices[level] += 1;
					// 尝试当前级偏移数组的下一个偏移
				}
				else {
					// 不是最后一级偏移
					if (result)
						level += 1;
					else
						offsetIndices[level] += 1;
				}
				while (true) {
					// 回溯
					if (offsetIndices[level] == offsetMatrix[level].Length) {
						// 如果当前级偏移尝试完成了
						if (level > 0) {
							// 回溯到上一级，清空当前级数据
							offsetIndices[level] = 0;
							level -= 1;
							offsetIndices[level] += 1;
						}
						else
							// 已经回溯到了level=0，说明扫描完成
							return pointers;
					}
					else
						break;
				}
			}
		}

		private static void* GetModuleHandle(Module module) {
			switch (Environment.Version.Major) {
			case 2:
				return (void*)(IntPtr)ModuleHandleField.GetValue(module.ModuleHandle);
			case 4:
				return (void*)(IntPtr)ModuleHandleField.GetValue(module);
			default:
				throw new NotSupportedException();
			}
		}

		private static bool TryToAddress(Pointer pointer, out void* address) {
			return NativeProcess.CurrentProcess.TryToAddress(pointer, out address);
		}

		private static bool TryReadUInt32(void* address, out uint value) {
			return NativeProcess.CurrentProcess.TryReadUInt32(address, out value);
		}

		private static bool TryReadIntPtr(void* address, out IntPtr value) {
			return NativeProcess.CurrentProcess.TryReadIntPtr(address, out value);
		}
	}
}
