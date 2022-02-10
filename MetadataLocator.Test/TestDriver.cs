using System;
using System.Linq;
using System.Reflection;

namespace MetadataLocator.Test;

public static unsafe class TestDriver {
	static int indent;

	public static void Test() {
		RuntimeDefinitionTests.VerifySize();
		RuntimeDefinitionTests.VerifyOffset();
		MetadataImportTests.Test();
		Print(Assembly.GetEntryAssembly().ManifestModule, true);
		Print(typeof(MetadataInfo).Module, true);
		RunTestAssemblys(5);
		Console.ReadKey(true);
	}

	static void RunTestAssemblys(int count) {
		for (int i = 0; i < count; i++) {
			for (TestAssemblyFlags inMemory = 0; inMemory <= TestAssemblyFlags.InMemory; inMemory += (int)TestAssemblyFlags.InMemory) {
				for (TestAssemblyFlags uncompressed = 0; uncompressed <= TestAssemblyFlags.Uncompressed; uncompressed += (int)TestAssemblyFlags.Uncompressed) {
					var assembly = TestAssemblyManager.GetAssembly((TestAssemblyFlags)i | inMemory | uncompressed);
					Print(assembly.Module, true);
				}
			}
		}
	}

	static void Print(Module module, bool end = false) {
		Print($"{module.Assembly.GetName().Name}: {{");
		indent++;
		Print(nameof(PEInfo), PEInfo.Create(module));
		Print(nameof(MetadataInfo), MetadataInfo.Create(module), true);
		indent--;
		Print(end ? "}" : "},");
	}

	static void Print(string name, PEInfo peInfo, bool end = false) {
		if (peInfo.IsInvalid) {
			Print(end ? $"{name}: null" : $"{name}: null,");
			return;
		}

		Print($"{name}: {{");
		indent++;
		Print($"{nameof(PEInfo.FilePath)}: {peInfo.FilePath},");
		Print($"{nameof(PEInfo.InMemory)}: {peInfo.InMemory},");
		Print(nameof(PEInfo.FlatLayout), peInfo.FlatLayout);
		Print(nameof(PEInfo.MappedLayout), peInfo.MappedLayout);
		Print(nameof(PEInfo.LoadedLayout), peInfo.LoadedLayout, true);
		indent--;
		Print(end ? "}" : "},");
	}

	static void Print(string name, PEImageLayout imageLayout, bool end = false) {
		if (imageLayout.IsInvalid) {
			Print(end ? $"{name}: null" : $"{name}: null,");
			return;
		}

		Print($"{name}: {{");
		indent++;
		Print($"{nameof(PEImageLayout.ImageBase)}: {FormatHex(imageLayout.ImageBase)},");
		Print($"{nameof(PEImageLayout.ImageSize)}: {FormatHex(imageLayout.ImageSize)},");
		Print($"{nameof(PEImageLayout.CorHeaderAddress)}: {FormatHex(imageLayout.CorHeaderAddress)}");
		indent--;
		Print(end ? "}" : "},");
	}

	static void Print(string name, MetadataInfo metadataInfo, bool end = false) {
		if (metadataInfo.IsInvalid) {
			Print(end ? $"{name}: null" : $"{name}: null,");
			return;
		}

		Print($"{name}: {{");
		indent++;
		Print($"{nameof(MetadataInfo.MetadataAddress)}: {FormatHex(metadataInfo.MetadataAddress)},");
		Print($"{nameof(MetadataInfo.MetadataSize)}: {FormatHex(metadataInfo.MetadataSize)},");
		Print(nameof(MetadataInfo.Schema), metadataInfo.Schema);
		Print(nameof(MetadataInfo.TableStream), metadataInfo.TableStream);
		Print(nameof(MetadataInfo.StringHeap), metadataInfo.StringHeap);
		Print(nameof(MetadataInfo.UserStringHeap), metadataInfo.UserStringHeap);
		Print(nameof(MetadataInfo.GuidHeap), metadataInfo.GuidHeap);
		Print(nameof(MetadataInfo.BlobHeap), metadataInfo.BlobHeap, true);
		indent--;
		Print(end ? "}" : "},");
	}

	static void Print(string name, MetadataSchema schema, bool end = false) {
		if (schema.IsEmpty) {
			Print(end ? $"{name}: null" : $"{name}: null,");
			return;
		}

		Print($"{name}: {{");
		indent++;
		Print($"{nameof(MetadataSchema.Reserved1)}: {schema.Reserved1},");
		Print($"{nameof(MetadataSchema.MajorVersion)}: {schema.MajorVersion},");
		Print($"{nameof(MetadataSchema.MinorVersion)}: {schema.MinorVersion},");
		Print($"{nameof(MetadataSchema.Flags)}: {schema.Flags},");
		Print($"{nameof(MetadataSchema.Log2Rid)}: {schema.Log2Rid},");
		Print($"{nameof(MetadataSchema.ValidMask)}: {FormatHex(schema.ValidMask)},");
		Print($"{nameof(MetadataSchema.SortedMask)}: {FormatHex(schema.SortedMask)},");
		Print($"{nameof(MetadataSchema.RowCounts)}: \"{string.Join(",", schema.RowCounts.Select(t => $"0x{t:X}").ToArray())}\",");
		Print($"{nameof(MetadataSchema.ExtraData)}: {schema.ExtraData}");
		indent--;
		Print(end ? "}" : "},");
	}

	static void Print(string name, MetadataTableInfo tableInfo, bool end = false) {
		if (tableInfo.IsEmpty) {
			Print(end ? $"{name}: null" : $"{name}: null,");
			return;
		}

		Print($"{name}: {{");
		indent++;
		Print($"{nameof(MetadataTableInfo.Address)}: {FormatHex(tableInfo.Address)},");
		Print($"{nameof(MetadataTableInfo.Length)}: {FormatHex(tableInfo.Length)},");
		Print($"{nameof(MetadataTableInfo.IsCompressed)}: {tableInfo.IsCompressed}");
		Print($"{nameof(MetadataTableInfo.TableCount)}: {tableInfo.TableCount},");
		Print($"{nameof(MetadataTableInfo.RowSizes)}: \"{string.Join(",", tableInfo.RowSizes.Select(t => $"0x{t:X}").ToArray())}\"");
		indent--;
		Print(end ? "}" : "},");
	}

	static void Print(string name, MetadataHeapInfo heapInfo, bool end = false) {
		if (heapInfo.IsEmpty) {
			Print($"{name}: null,");
			return;
		}

		Print($"{name}: {{");
		indent++;
		Print($"{nameof(MetadataStreamInfo.Address)}: {FormatHex(heapInfo.Address)},");
		Print($"{nameof(MetadataStreamInfo.Length)}: {FormatHex(heapInfo.Length)}");
		indent--;
		Print(end ? "}" : "},");
	}

	static void Print(string value) {
		Console.WriteLine(new string(' ', indent * 2) + value);
	}

	static string FormatHex(uint value) {
		return $"0x{value:X8}";
	}

	static string FormatHex(ulong value) {
		return $"0x{value:X16}";
	}

	static string FormatHex(nuint value) {
		return sizeof(nuint) == 4 ? FormatHex((uint)value) : FormatHex((ulong)value);
	}
}
