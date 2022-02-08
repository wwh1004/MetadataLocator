namespace MetadataLocator;

#pragma warning disable CS0649
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
public static unsafe class RuntimeDefinitions {
	#region Basic
	public struct IMAGE_DATA_DIRECTORY {
		public uint VirtualAddress;
		public uint Size;
	}

	public struct IMAGE_COR20_HEADER {
		public uint cb;
		public ushort MajorRuntimeVersion;
		public ushort MinorRuntimeVersion;
		public IMAGE_DATA_DIRECTORY MetaData;
		public uint Flags;
		public uint EntryPointTokenOrRVA;
		public IMAGE_DATA_DIRECTORY Resources;
		public IMAGE_DATA_DIRECTORY StrongNameSignature;
		public IMAGE_DATA_DIRECTORY CodeManagerTable;
		public IMAGE_DATA_DIRECTORY VTableFixups;
		public IMAGE_DATA_DIRECTORY ExportAddressTableJumps;
		public IMAGE_DATA_DIRECTORY ManagedNativeHeader;
	};

	// ==========================================================================================
	// SString is the base class for safe strings.
	// ==========================================================================================
	/* complete, 0x10 / 0x18 bytes */
	public struct SString {
		/* important */
		public uint m_size;
		public uint m_allocation;
		public uint m_flags;
		/* important */
		public nuint m_buffer;
	}

	/* complete, 0x1c / 0x30 bytes */
	public struct Crst {
		public nuint __padding_0;
		public uint __padding_1;
		public uint __padding_2;
		public nuint __padding_3;
		public nuint __padding_4;
		public nuint __padding_5;
		public uint __padding_6;
	}
	#endregion

	#region PE
	//public const int IMAGE_FLAT = 0;
	//public const int IMAGE_MAPPED = 1;  // comment: not existing in newer clr
	//public const int IMAGE_LOADED = 2;
	//public const int IMAGE_LOADED_FOR_INTROSPECTION = 3;  // comment: not existing in newer clr
	//public const int IMAGE_COUNT = 4;

	// --------------------------------------------------------------------------------
	// PEDecoder - Utility class for reading and verifying PE files.
	//
	// Note that the Check step is optional if you are willing to trust the
	// integrity of the image.
	// (Or at any rate can be factored into an initial verification step.)
	//
	// Functions which access the memory of the PE file take a "flat" flag - this
	// indicates whether the PE images data has been loaded flat the way it resides in the file,
	// or if the sections have been mapped into memory at the proper base addresses.
	//
	// Finally, some functions take an optional "size" argument, which can be used for
	// range verification.  This is an optional parameter, but if you omit it be sure
	// you verify the size in some other way.
	// --------------------------------------------------------------------------------
	/* complete, 0x18 / 0x28 bytes */
	public struct PEDecoder {
		/* important */
		public nuint m_base;
		/* important */
		public uint m_size; // size of file on disk, as opposed to OptionalHeaders.SizeOfImage
		public uint m_flags;
		public nuint m_pNTHeaders;
		/* important */
		public nuint m_pCorHeader;
		public nuint m_pNativeHeader;
		// public nuint m_pReadyToRunHeader; // comment: only in coreclr
	}

	/* incomplete */
	public struct PEImageLayout {
		public nuint __vfptr;
		public PEDecoder __base;

		// ... some paddings ...
	}

	// --------------------------------------------------------------------------------
	// PEImage is a PE file loaded by our "simulated LoadLibrary" mechanism.  A PEImage
	// can be loaded either FLAT (same layout as on disk) or MAPPED (PE sections
	// mapped into virtual addresses.)
	//
	// The MAPPED format is currently limited to "IL only" images - this can be checked
	// for via PEDecoder::IsILOnlyImage.
	//
	// NOTE: PEImage will NEVER call LoadLibrary.
	// --------------------------------------------------------------------------------
	/* incomplete */
	public struct PEImage {
	}

	/* incomplete */
	public struct PEImage_20 {
		public Crst m_PdbStreamLock;
		public nuint m_pPdbStream;
		/* important */
		public SString m_path;

		// ... some paddings ...
		/* important */
		//public fixed nuint m_pLayouts[IMAGE_COUNT];
		//public bool m_bInHashMap;
		//public nuint m_pMDTracker; // comments: might exists
		/* important */
		//public nuin m_pMDImport;
	}

	/* incomplete */
	public struct PEImage_40 {
		/* important */
		public SString m_path;

		// ... some paddings ...
		/* important */
		//public fixed nuint m_pLayouts[IMAGE_COUNT];
		//public bool m_bInHashMap;
		//public nuint m_pMDTracker; // comments: might exists
		/* important */
		//public nuin m_pMDImport;
	}

	/* incomplete */
	public struct PEFile {
		public nuint __vfptr;
		public PEImage* m_identity;      // Identity image
		/* important */
		public PEImage* m_openedILimage; // IL image, NULL if we didn't need to open the file

		// ... some paddings ...
		/* important */
		//public nuint m_pMDImport;
	}
	#endregion

	#region StgPool
	/* complete, 0x10 / 0x18 bytes */
	public struct StgPoolSeg {
		/* important */
		public byte* m_pSegData;       // Pointer to the data.
		public StgPoolSeg* m_pNextSeg; // Pointer to next segment, or NULL.
		/* important */
		public uint m_cbSegSize;       // Size of the segment buffer. If this is last segment (code:m_pNextSeg is NULL), then it's the
									   // allocation size. If this is not the last segment, then this is shrinked to segment data size 
									   // (code:m_cbSegNext).
		public uint m_cbSegNext;       // Offset of next available byte in segment. Segment relative.
	}

	/* complete, 0x18 / 0x28 bytes */
	public struct StgPoolReadOnly {
		public nuint __vfptr;
		public StgPoolSeg __base;
		public nuint m_HotHeap;
	}

	/* complete, 0x18 / 0x28 bytes */
	public struct StgBlobPoolReadOnly {
		public StgPoolReadOnly __base;
	}

	//public struct StgPool {
	//	public StgPoolReadOnly __base;
	//	public uint m_ulGrowInc;              // How many bytes at a time.
	//	public StgPoolSeg* m_pCurSeg;         // Current seg for append -- end of chain.
	//	public uint m_cbCurSegOffset;         // Base offset of current seg.
	//	public uint m_bFree_or_bReadOnly;     // True if we should free base data. Extension data is always freed.
	//										  // True if we shouldn't append.
	//	public uint m_nVariableAlignmentMask; // Alignment mask (variable 0, 1 or 3).
	//	public uint m_cbStartOffsetOfEdit;    // Place in the pool where edits started
	//	public int m_fValidOffsetOfEdit;      // Is the pool edit offset valid
	//}
	#endregion

	#region Metadata Heap
	/* complete, 0x18 / 0x28 bytes */
	public struct StringHeapRO {
		public StgPoolReadOnly m_StringPool;
	}

	/* complete, 0x18 / 0x28 bytes */
	public struct BlobHeapRO {
		public StgBlobPoolReadOnly m_BlobPool;
	}

	/* complete, 0x18 / 0x28 bytes */
	public struct GuidHeapRO {
		public StgPoolReadOnly m_GuidPool;
	}
	#endregion

	#region Metadata Model
	public const int TBL_Module = 0;
	public const int TBL_TypeRef = 1;
	public const int TBL_TypeDef = 2;
	public const int TBL_FieldPtr = 3;
	public const int TBL_Field = 4;
	public const int TBL_MethodPtr = 5;
	public const int TBL_Method = 6;
	public const int TBL_ParamPtr = 7;
	public const int TBL_Param = 8;
	public const int TBL_InterfaceImpl = 9;
	public const int TBL_MemberRef = 10;
	public const int TBL_Constant = 11;
	public const int TBL_CustomAttribute = 12;
	public const int TBL_FieldMarshal = 13;
	public const int TBL_DeclSecurity = 14;
	public const int TBL_ClassLayout = 15;
	public const int TBL_FieldLayout = 16;
	public const int TBL_StandAloneSig = 17;
	public const int TBL_EventMap = 18;
	public const int TBL_EventPtr = 19;
	public const int TBL_Event = 20;
	public const int TBL_PropertyMap = 21;
	public const int TBL_PropertyPtr = 22;
	public const int TBL_Property = 23;
	public const int TBL_MethodSemantics = 24;
	public const int TBL_MethodImpl = 25;
	public const int TBL_ModuleRef = 26;
	public const int TBL_TypeSpec = 27;
	public const int TBL_ImplMap = 28;
	public const int TBL_FieldRVA = 29;
	public const int TBL_ENCLog = 30;
	public const int TBL_ENCMap = 31;
	public const int TBL_Assembly = 32;
	public const int TBL_AssemblyProcessor = 33;
	public const int TBL_AssemblyOS = 34;
	public const int TBL_AssemblyRef = 35;
	public const int TBL_AssemblyRefProcessor = 36;
	public const int TBL_AssemblyRefOS = 37;
	public const int TBL_File = 38;
	public const int TBL_ExportedType = 39;
	public const int TBL_ManifestResource = 40;
	public const int TBL_NestedClass = 41;
	public const int TBL_GenericParam = 42;
	public const int TBL_MethodSpec = 43;
	public const int TBL_GenericParamConstraint = 44;
	public const int TBL_COUNT = 45;    // Highest table.
	public const int TBL_COUNT_V1 = 42; // Highest table in v1.0 database
	public const int TBL_COUNT_V2 = 45; // Highest in v2.0 database

	//*****************************************************************************
	// The mini, hard-coded schema.  For each table, we persist the count of
	//  records.  We also persist the size of string, blob, guid, and rid
	//  columns.  From this information, we can calculate the record sizes, and
	//  then the sizes of the tables.
	//*****************************************************************************
	/* complete, 0x18 / 0x18 bytes */
	public struct CMiniMdSchemaBase {
		public uint m_ulReserved; // Reserved, must be zero.
		public byte m_major;      // Version numbers.
		public byte m_minor;
		public byte m_heaps;      // Bits for heap sizes.
		public byte m_rid;        // log-base-2 of largest rid.
		public ulong m_maskvalid; // Bit mask of present table counts.
		public ulong m_sorted;    // Bit mask of sorted tables.
	}

	/* complete, 0xd0 / 0xd0 bytes */
	public struct CMiniMdSchema {
		public CMiniMdSchemaBase __base;
		public fixed uint m_cRecs[TBL_COUNT]; // Counts of various tables.
		public uint m_ulExtra;                // Extra data, only persisted if non-zero.  (m_heaps&EXTRA_DATA flags)
	}

	/* incomplete */
	public struct CMiniMdBase {
		public nuint __vfptr;
		public CMiniMdSchema m_Schema;         // data header.
		public uint m_TblCount;                // Tables in this database.
		public int m_fVerifiedByTrustedSource; // whether the data was verified by a trusted source

		// ... some paddings ...
	}

	/* incomplete */
	public struct CMiniMd {
		public CMiniMdBase __base;
		// class MetaData::TableRO m_Tables[45];
		//public StringHeapRO m_StringHeap;
		//public BlobHeapRO m_BlobHeap;
		//public BlobHeapRO m_UserStringHeap;
		//public GuidHeapRO m_GuidHeap;
	}

	public struct CMiniMdRW {
		public CMiniMdBase __base;

		// ... some paddings ...
	}
	#endregion

	#region LiteWeightStgdb
	public struct CLiteWeightStgdb_CMiniMd {
		public CMiniMd m_MiniMd;

		// ... some paddings ...
		//public nuint m_pvMd;
		//public uint m_cbMd;
	}

	public struct CLiteWeightStgdb_CMiniMdRW {
		public CMiniMdRW m_MiniMd;

		// ... some paddings ...
		//public nuint m_pvMd;
		//public uint m_cbMd;
	}

	public struct CLiteWeightStgdbRW {
		public CLiteWeightStgdb_CMiniMdRW __base;

		// ... some paddings ...
	}
	#endregion

	#region Metadata Internal
	public struct MDInternalRO {
	}

	public struct MDInternalRO_20 {
		public nuint __vfptr_IMDInternalImport;
		public CLiteWeightStgdb_CMiniMd m_LiteWeightStgdb;
	}

	public struct MDInternalRO_45 {
		public nuint __vfptr_IMDInternalImport;
		public nuint __vfptr_IMDCommon;
		public CLiteWeightStgdb_CMiniMd m_LiteWeightStgdb;
	}

	public struct MDInternalRW {
	}

	public struct MDInternalRW_20 {
		public nuint __vfptr_IMDInternalImport;
		public CLiteWeightStgdbRW* m_pStgdb;
	}

	public struct MDInternalRW_45 {
		public nuint __vfptr_IMDInternalImport;
		public nuint __vfptr_IMDCommon;
		public CLiteWeightStgdbRW* m_pStgdb;
	}
	#endregion

	#region Reflection
	/* incomplete */
	public struct Module {
	}

	/* incomplete */
	public struct Module_20 {
		public nuint __vfptr;
		/* important */
		public PEFile* m_file;
	}

	/* incomplete */
	public struct Module_453 {
		public nuint __vfptr;
		public nuint m_pSimpleName;
		/* important */
		public PEFile* m_file;
	}
	#endregion
}
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#pragma warning restore CS0649
