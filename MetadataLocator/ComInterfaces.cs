#pragma warning disable CS1591
using System;
using System.Runtime.InteropServices;

namespace MetadataLocator;

[ComImport]
[Guid("D8F579AB-402D-4B8E-82D9-5D63B1065C68")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IMetaDataTables {
	[PreserveSig]
	int GetStringHeapSize(out uint pcbStrings);

	[PreserveSig]
	int GetBlobHeapSize(out uint pcbBlobs);

	[PreserveSig]
	int GetGuidHeapSize(out uint pcbGuids);

	[PreserveSig]
	int GetUserStringHeapSize(out uint pcbBlobs);

	[PreserveSig]
	int GetNumTables(out uint pcTables);

	[PreserveSig]
	int GetTableIndex(uint token, out uint pixTbl);

	[PreserveSig]
	int GetTableInfo(uint ixTbl, out uint pcbRow, out uint pcRows, out uint pcCols, out uint piKey, out nuint ppName);

	[PreserveSig]
	int GetColumnInfo(uint ixTbl, uint ixCol, out uint poCol, out uint pcbCol, out uint pType, out nuint ppName);

	[PreserveSig]
	int GetCodedTokenInfo(uint ixCdTkn, out uint pcTokens, out nuint ppTokens, out nuint ppName);

	[PreserveSig]
	int GetRow(uint ixTbl, uint rid, out nuint ppRow);

	[PreserveSig]
	int GetColumn(uint ixTbl, uint ixCol, uint rid, out uint pVal);

	[PreserveSig]
	int GetString(uint ixString, out nuint ppString);

	[PreserveSig]
	int GetBlob(uint ixBlob, out uint pcbData, out nuint ppData);

	[PreserveSig]
	int GetGuid(uint ixGuid, out nuint ppGUID);

	[PreserveSig]
	int GetUserString(uint ixUserString, out uint pcbData, out nuint ppData);

	[PreserveSig]
	int GetNextString(uint ixString, out uint pNext);

	[PreserveSig]
	int GetNextBlob(uint ixBlob, out uint pNext);

	[PreserveSig]
	int GetNextGuid(uint ixGuid, out uint pNext);

	[PreserveSig]
	int GetNextUserString(uint ixUserString, out uint pNext);
}
#pragma warning restore CS1591
