using System.Runtime.InteropServices;

namespace MetadataLocator.NativeSharp;

static class NativeMethods {
	[DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, SetLastError = true)]
	public static extern nuint GetCurrentProcess();

	[DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, SetLastError = true)]
	public static extern bool ReadProcessMemory(nuint hProcess, nuint lpBaseAddress, nuint lpBuffer, uint nSize, out uint lpNumberOfBytesRead);

	[DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, SetLastError = true)]
	public static extern nuint GetModuleHandle(string lpModuleName);
}
