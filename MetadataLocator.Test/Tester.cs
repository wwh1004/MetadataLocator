using System;

namespace MetadataLocator.Test {
	public static unsafe class Tester {
		public static void Test() {
			MetadataInfo metadataInfo;

			metadataInfo = new MetadataInfo(typeof(MetadataInfo).Module);
			PrintStreamInfo("#~ or #-", metadataInfo.TableStream);
			PrintStreamInfo("#Strings", metadataInfo.StringHeap);
			PrintStreamInfo("#US", metadataInfo.UserStringHeap);
			PrintStreamInfo("#GUID", metadataInfo.GuidHeap);
			PrintStreamInfo("#Blob", metadataInfo.BlobHeap);
			Console.ReadKey(true);
		}

		private static void PrintStreamInfo(string name, MetadataStreamInfo streamInfo) {
			Console.WriteLine($"Name: {name}");
			if (streamInfo == null) {
				Console.WriteLine("Not exists.");
			}
			else {
				Console.WriteLine($"Address: 0x{((IntPtr)streamInfo.Address).ToString(IntPtr.Size == 4 ? "X8" : "X16")}");
				Console.WriteLine($"Length: 0x{streamInfo.Length.ToString("X8")}");
			}
			Console.WriteLine();
		}
	}
}
