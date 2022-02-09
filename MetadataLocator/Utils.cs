using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace MetadataLocator;

static unsafe class Utils {
	public static void Check(RuntimeDefinitions.Module* module, string moduleName) {
		Check(Memory.TryReadUIntPtr((nuint)module, out _));
		if (RuntimeEnvironment.Version >= RuntimeVersion.Fx453) {
			nuint m_pSimpleName = ((RuntimeDefinitions.Module_453*)module)->m_pSimpleName;
			bool b = Memory.TryReadUtf8String(m_pSimpleName, out var simpleName) && simpleName == moduleName;
			Check(b);
		}
	}

	public static void Check(RuntimeDefinitions.PEFile* file) {
		Check(Memory.TryReadUIntPtr((nuint)file, out _));
	}

	public static void Check(RuntimeDefinitions.PEImage* image, bool inMemory) {
		Check(Memory.TryReadUIntPtr((nuint)image, out _));
		if (RuntimeEnvironment.Version >= RuntimeVersion.Fx40) {
			var m_Path = ((RuntimeDefinitions.PEImage_40*)image)->m_path;
			bool b = Memory.TryReadUnicodeString(m_Path.m_buffer, out var path) && (inMemory ? path.Length == 0 : File.Exists(path));
			Check(b);
		}
		else {
			var m_Path = ((RuntimeDefinitions.PEImage_20*)image)->m_path;
			bool b = Memory.TryReadUnicodeString(m_Path.m_buffer, out var path) && (inMemory ? path.Length == 0 : File.Exists(path));
			Check(b);
		}
	}

	public static void Check(RuntimeDefinitions.PEImageLayout* layout, bool inMemory) {
		Check(Memory.TryReadUIntPtr((nuint)layout, out _));
		Check((ushort)layout->__base.m_base == 0);
		Check(layout->__base.m_pCorHeader - layout->__base.m_base == (inMemory ? 0x208u : 0x2008));
	}

	public static void Check(RuntimeDefinitions.IMAGE_COR20_HEADER* corHeader) {
		Check(Memory.TryReadUIntPtr((nuint)corHeader, out _));
		Check(corHeader->cb == 0x48);
		Check(corHeader->MajorRuntimeVersion == 2);
		Check(corHeader->MinorRuntimeVersion == 2);
	}

	public static void Check(RuntimeDefinitions.STORAGESIGNATURE* storageSignature) {
		Check(Memory.TryReadUIntPtr((nuint)storageSignature, out _));
		Check(storageSignature->lSignature == RuntimeDefinitions.STORAGE_MAGIC_SIG);
		Check(storageSignature->iMajorVer == 1);
		Check(storageSignature->iMinorVer == 1);
	}

	public static void Check([DoesNotReturnIf(false)] bool condition) {
		if (!condition) {
			Debug2.Assert(false, "Contains error in RuntimeDefinitions");
			throw new InvalidOperationException("Contains error in RuntimeDefinitions");
		}
	}

	public static bool Verify(Pointer template, bool? testUncompressed, Predicate<nuint> checker) {
		for (int i = 0; i < 5; i++) {
			for (TestAssemblyFlags inMemory = 0; inMemory <= TestAssemblyFlags.InMemory; inMemory += (int)TestAssemblyFlags.InMemory) {
				var uncompressed = testUncompressed == true ? TestAssemblyFlags.Uncompressed : 0;
				var uncompressedEnd = testUncompressed == false ? 0 : TestAssemblyFlags.Uncompressed;
				for (; uncompressed <= uncompressedEnd; uncompressed += (int)TestAssemblyFlags.Uncompressed) {
					var assembly = TestAssemblyManager.GetAssembly((TestAssemblyFlags)i | inMemory | uncompressed);
					nuint value = ReadUIntPtr(template, assembly.ModuleHandle);
					if (value == 0 || !checker(value))
						return false;
				}
			}
		}
		return true;
	}

	public static uint ReadUInt32(Pointer template, nuint baseAddress) {
		nuint address = ReadPointer(template, baseAddress);
		if (!Memory.TryReadUInt32(address, out uint value))
			return default;
		return value;
	}

	public static nuint ReadUIntPtr(Pointer template, nuint baseAddress) {
		nuint address = ReadPointer(template, baseAddress);
		if (!Memory.TryReadUIntPtr(address, out nuint value))
			return default;
		return value;
	}

	public static string ReadUnicodeString(Pointer template, nuint baseAddress) {
		nuint address = ReadPointer(template, baseAddress);
		if (!Memory.TryReadUnicodeString(address, out var value))
			return string.Empty;
		return value;
	}

	static nuint ReadPointer(Pointer template, nuint baseAddress) {
		if (template.IsEmpty)
			return default;
		var pointer = MakePointer(template, baseAddress);
		if (!Memory.TryToAddress(pointer, out nuint address))
			return default;
		return address;
	}

	static Pointer MakePointer(Pointer template, nuint baseAddress) {
		Debug2.Assert(!template.IsEmpty);
		return new Pointer(template) { BaseAddress = baseAddress };
	}
}
