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
		Print(Assembly.GetEntryAssembly().ManifestModule);
		Print(typeof(MetadataInfo).Module);
		Console.ReadKey(true);
	}

	static void Print(Module module) {
		Print($"{module.Assembly.GetName().Name}: {{");
		indent++;
		var peInfo = PEInfo.Create(module);
		Print(nameof(PEInfo), peInfo);
		var metadataInfo = MetadataInfo.Create(module);
		Print(nameof(MetadataInfo), metadataInfo);
		indent--;
		Print("},");
	}

	static void Print(string name, PEInfo peInfo) {
		if (peInfo.IsInvalid) {
			Print($"{name}: null,");
			return;
		}

		Print($"{name}: {{");
		indent++;
		Print($"{nameof(PEInfo.FilePath)}: {peInfo.FilePath},");
		Print($"{nameof(PEInfo.InMemory)}: {peInfo.InMemory},");
		Print(nameof(PEInfo.FlatLayout), peInfo.FlatLayout);
		Print(nameof(PEInfo.MappedLayout), peInfo.MappedLayout);
		Print(nameof(PEInfo.LoadedLayout), peInfo.LoadedLayout);
		indent--;
		Print("},");
	}

	static void Print(string name, PEImageLayout imageLayout) {
		if (imageLayout.IsInvalid) {
			Print($"{name}: null,");
			return;
		}

		Print($"{name}: {{");
		indent++;
		Print($"{nameof(PEImageLayout.ImageBase)}: {FormatHex(imageLayout.ImageBase)},");
		Print($"{nameof(PEImageLayout.ImageSize)}: {FormatHex(imageLayout.ImageSize)},");
		Print($"{nameof(PEImageLayout.CorHeaderAddress)}: {FormatHex(imageLayout.CorHeaderAddress)},");
		indent--;
		Print("},");
	}

	static void Print(string name, MetadataInfo metadataInfo) {
		if (metadataInfo.IsInvalid) {
			Print($"{name}: null,");
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
		Print(nameof(MetadataInfo.BlobHeap), metadataInfo.BlobHeap);
		indent--;
		Print("},");
	}

	static void Print(string name, MetadataSchema schema) {
		if (schema.IsEmpty) {
			Print($"{name}: null,");
			return;
		}

		Print($"{name}: {{");
		indent++;
		Print($"{nameof(MetadataSchema.MajorVersion)}: {schema.MajorVersion},");
		Print($"{nameof(MetadataSchema.MinorVersion)}: {schema.MinorVersion},");
		Print($"{nameof(MetadataSchema.Flags)}: {schema.Flags},");
		Print($"{nameof(MetadataSchema.Log2Rid)}: {schema.Log2Rid},");
		Print($"{nameof(MetadataSchema.ValidMask)}: {FormatHex(schema.ValidMask)},");
		Print($"{nameof(MetadataSchema.SortedMask)}: {FormatHex(schema.SortedMask)},");
		Print($"{nameof(MetadataSchema.Rows)}: \"{string.Join(",", schema.Rows.Select(t => t.ToString()).ToArray())}\",");
		indent--;
		Print("},");
	}

	static void Print(string name, MetadataStreamInfo streamInfo) {
		if (streamInfo.IsEmpty) {
			Print($"{name}: null,");
			return;
		}

		Print($"{name}: {{");
		indent++;
		Print($"{nameof(MetadataStreamInfo.Address)}: {FormatHex(streamInfo.Address)},");
		Print($"{nameof(MetadataStreamInfo.Length)}: {FormatHex(streamInfo.Length)},");
		if (streamInfo is MetadataTableInfo tableStream)
			Print($"{nameof(MetadataTableInfo.IsCompressed)}: {tableStream.IsCompressed},");
		indent--;
		Print("},");
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
