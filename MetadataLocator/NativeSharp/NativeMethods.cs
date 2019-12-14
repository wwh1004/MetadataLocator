using System.Runtime.InteropServices;
using System.Text;

namespace MetadataLocator.NativeSharp {
	internal static unsafe class NativeMethods {
		public const uint LIST_MODULES_ALL = 0x3;
		public const uint MAX_MODULE_NAME32 = 255;

		[DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, SetLastError = true)]
		public static extern uint GetCurrentProcessId();

		[DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, SetLastError = true)]
		public static extern void* GetCurrentProcess();

		[DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool ReadProcessMemory(void* hProcess, void* lpBaseAddress, void* lpBuffer, uint nSize, uint* lpNumberOfBytesRead);

		[DllImport("psapi.dll", BestFitMapping = false, CharSet = CharSet.Unicode, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool GetModuleBaseName(void* hProcess, void* hModule, StringBuilder lpBaseName, uint nSize);

		[DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, SetLastError = true)]
		public static extern void* GetModuleHandle(string lpModuleName);
	}
}
