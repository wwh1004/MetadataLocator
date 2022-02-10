using System;
using System.Collections.Generic;
using static MetadataLocator.RuntimeDefinitions;

namespace MetadataLocator.Test;

static unsafe class RuntimeDefinitionTests {
	readonly struct ArchSize {
		public readonly ushort X86;
		public readonly ushort X64;

		public ArchSize(ushort x86, ushort x64) {
			X86 = x86;
			X64 = x64;
		}
	}

	static readonly Dictionary<Type, ArchSize> sizeMap = new() {
		[typeof(SString)] = new(0x10, 0x18),
		[typeof(Crst)] = new(0x1c, 0x30),
		[typeof(PEDecoder)] = new(0x18, 0x28),
		[typeof(StgPoolReadOnly)] = new(0x18, 0x28),
		[typeof(StgBlobPoolReadOnly)] = new(0x18, 0x28),
		[typeof(TableRO)] = new(0x04, 0x08),
		[typeof(TableROs)] = new(0x00b4, 0x0168),
		[typeof(StringHeapRO)] = new(0x18, 0x28),
		[typeof(BlobHeapRO)] = new(0x18, 0x28),
		[typeof(GuidHeapRO)] = new(0x18, 0x28),
		[typeof(CMiniMdSchemaBase)] = new(0x18, 0x18),
		[typeof(CMiniMdSchema)] = new(0xd0, 0xd0),
		[typeof(CMiniColDef)] = new(0x03, 0x03),
		[typeof(CMiniTableDef)] = new(0x08, 0x10),
		[typeof(CMiniTableDefs)] = new(0x0168, 0x02d0),
		[typeof(CMiniMdBase_20)] = new(0x0250, 0x03c0),
		[typeof(CMiniMdBase_40)] = new(0x0258, 0x03c0),
		[typeof(CMiniMd_20)] = new(0x0368, 0x05c8),
		[typeof(CMiniMd_40)] = new(0x0370, 0x05c8),
		[typeof(CLiteWeightStgdb_CMiniMd_20)] = new(0x0370, 0x05d8),
		[typeof(CLiteWeightStgdb_CMiniMd_40)] = new(0x0378, 0x05d8),
		[typeof(MDInternalRO_20)] = new(0x0378, 0x05e0),
		[typeof(MDInternalRO_40)] = new(0x0380, 0x05e0),
		[typeof(MDInternalRO_45)] = new(0x0380, 0x05e8),
	};

	public static void VerifySize() {
		foreach (var sizeEntry in sizeMap) {
			uint actualSize = Utils.SizeOf(sizeEntry.Key);
			uint expectedSize = sizeof(nuint) == 4 ? sizeEntry.Value.X86 : sizeEntry.Value.X64;
			Assert.IsTrue(actualSize == expectedSize, $"Expected size 0x{expectedSize:X} bytes but got 0x{actualSize:X} bytes for {sizeEntry.Key.Name}");
		}
	}
}
