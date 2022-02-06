using System.Collections.Generic;

namespace MetadataLocator.NativeSharp;

sealed class Pointer {
	public nuint BaseAddress { get; set; }

	public IList<uint> Offsets { get; }

	public Pointer(nuint baseAddress, params uint[] offsets) {
		BaseAddress = baseAddress;
		Offsets = new List<uint>(offsets);
	}

	public Pointer(Pointer pointer) {
		BaseAddress = pointer.BaseAddress;
		Offsets = new List<uint>(pointer.Offsets);
	}
}
