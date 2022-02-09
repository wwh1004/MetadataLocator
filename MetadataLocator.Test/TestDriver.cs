using System;

namespace MetadataLocator.Test;

public static unsafe class TestDriver {
	public static void Test() {
		RuntimeDefinitionTests.VerifySize();
		MetadataImportTests.Test();
		var testModule = typeof(MetadataInfo).Module;
		var peInfo = PEInfo.Create(testModule);
		PrintPEInfo(nameof(PEInfo), peInfo);
		var metadataInfo = MetadataInfo.Create(testModule);
		PrintMetadataInfo(nameof(MetadataInfo), metadataInfo);
		Console.ReadKey(true);
	}

	static void PrintPEInfo(string name, PEInfo peInfo) {
		if (peInfo.IsInvalid) {
			WriteLine($"{name}: null,");
			return;
		}

		WriteLine($"{name}: {{");
		indent++;
		WriteLine($"{nameof(PEInfo.FilePath)}: {peInfo.FilePath},");
		WriteLine($"{nameof(PEInfo.InMemory)}: {peInfo.InMemory},");
		PrintImageLayout(nameof(PEInfo.FlatLayout), peInfo.FlatLayout);
		PrintImageLayout(nameof(PEInfo.MappedLayout), peInfo.MappedLayout);
		PrintImageLayout(nameof(PEInfo.LoadedLayout), peInfo.LoadedLayout);
		indent--;
		WriteLine("},");
	}

	static void PrintImageLayout(string name, PEImageLayout imageLayout) {
		if (imageLayout.IsInvalid) {
			WriteLine($"{name}: null,");
			return;
		}

		WriteLine($"{name}: {{");
		indent++;
		WriteLine($"{nameof(PEImageLayout.ImageBase)}: {FormatHex(imageLayout.ImageBase)},");
		WriteLine($"{nameof(PEImageLayout.ImageSize)}: {FormatHex(imageLayout.ImageSize)},");
		WriteLine($"{nameof(PEImageLayout.CorHeaderAddress)}: {FormatHex(imageLayout.CorHeaderAddress)},");
		indent--;
		WriteLine("},");
	}

	static void PrintMetadataInfo(string name, MetadataInfo metadataInfo) {
		if (metadataInfo.IsInvalid) {
			WriteLine($"{name}: null,");
			return;
		}

		WriteLine($"{name}: {{");
		indent++;
		PrintStreamInfo(nameof(MetadataInfo.TableStream), metadataInfo.TableStream);
		PrintStreamInfo(nameof(MetadataInfo.StringHeap), metadataInfo.StringHeap);
		PrintStreamInfo(nameof(MetadataInfo.UserStringHeap), metadataInfo.UserStringHeap);
		PrintStreamInfo(nameof(MetadataInfo.GuidHeap), metadataInfo.GuidHeap);
		PrintStreamInfo(nameof(MetadataInfo.BlobHeap), metadataInfo.BlobHeap);
		indent--;
		WriteLine("},");
	}

	static void PrintStreamInfo(string name, MetadataStreamInfo streamInfo) {
		if (streamInfo.IsEmpty) {
			WriteLine($"{name}: null,");
			return;
		}

		WriteLine($"{name}: {{");
		indent++;
		WriteLine($"{nameof(MetadataStreamInfo.Address)}: {FormatHex(streamInfo.Address)},");
		WriteLine($"{nameof(MetadataStreamInfo.Length)}: {FormatHex(streamInfo.Length)},");
		if (streamInfo is MetadataTableInfo tableStream)
			WriteLine($"{nameof(MetadataTableInfo.IsCompressed)}: {tableStream.IsCompressed},");
		indent--;
		WriteLine("},");
	}

	static int indent;

	static void WriteLine(string value) {
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
