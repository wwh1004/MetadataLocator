using System;
using System.Runtime.InteropServices;

namespace MetadataLocator;

static class NativeMethods {
	public static readonly Guid IID_IMetaDataTables = new(0xD8F579AB, 0x402D, 0x4B8E, 0x82, 0xD9, 0x5D, 0x63, 0xB1, 0x06, 0x5C, 0x68);

	public static readonly Guid IID_IMetaDataTables2 = new(0xBADB5F70, 0x58DA, 0x43A9, 0xA1, 0xC6, 0xD7, 0x48, 0x19, 0xF1, 0x9B, 0x15);

	[DllImport("mscorwks.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "GetMetaDataPublicInterfaceFromInternal", SetLastError = true)]
	public static extern int GetMetaDataPublicInterfaceFromInternal2(nuint pv, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out nuint ppv);

	[DllImport("clr.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "GetMetaDataPublicInterfaceFromInternal", SetLastError = true)]
	public static extern int GetMetaDataPublicInterfaceFromInternal4(nuint pv, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out nuint ppv);
}
