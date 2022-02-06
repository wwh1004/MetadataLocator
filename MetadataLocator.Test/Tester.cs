using System;

namespace MetadataLocator.Test;

public static unsafe class Tester {
	public static void Test() {
		var metadataInfo = MetadataInfo.GetMetadataInfo(typeof(MetadataInfo).Module);
		var dotNetPEInfo = metadataInfo.PEInfo;
		PrintStreamInfo("#~ or #-", metadataInfo.TableStream);
		PrintStreamInfo("#Strings", metadataInfo.StringHeap);
		PrintStreamInfo("#US", metadataInfo.UserStringHeap);
		PrintStreamInfo("#GUID", metadataInfo.GuidHeap);
		PrintStreamInfo("#Blob", metadataInfo.BlobHeap);
		Console.WriteLine($"DotNetPEInfo.IsValid: {dotNetPEInfo.IsValid}");
		if (dotNetPEInfo.IsValid) {
			Console.WriteLine($"DotNetPEInfo.ImageLayout: {dotNetPEInfo.ImageLayout}");
			Console.WriteLine($"DotNetPEInfo.Cor20HeaderAddress: {dotNetPEInfo.Cor20HeaderAddress.ToHexString()}");
			Console.WriteLine($"DotNetPEInfo.MetadataAddress: {dotNetPEInfo.MetadataAddress.ToHexString()}");
			Console.WriteLine($"DotNetPEInfo.MetadataSize: {dotNetPEInfo.MetadataSize}");
		}
		Console.ReadKey(true);
	}

	static void PrintStreamInfo(string name, MetadataStreamInfo streamInfo) {
		Console.WriteLine($"Name: {name}");
		if (streamInfo == null) {
			Console.WriteLine("Not exists.");
		}
		else {
			Console.WriteLine($"Address: 0x{streamInfo.Address.ToHexString()}");
			Console.WriteLine($"Length: 0x0{streamInfo.Length:X8}");
		}
		Console.WriteLine();
	}

	static string ToHexString(this nuint intPtr) {
		return ((nint)intPtr).ToString(sizeof(nuint) == 4 ? "X8" : "X16");
	}
}
