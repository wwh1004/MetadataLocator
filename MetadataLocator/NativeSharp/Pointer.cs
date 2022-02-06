using System.Collections.Generic;

namespace MetadataLocator.NativeSharp;

unsafe sealed class Pointer {
	public void* BaseAddress { get; set; }

	public IList<uint> Offsets { get; }

	public Pointer(void* baseAddress, params uint[] offsets) {
		BaseAddress = baseAddress;
		Offsets = new List<uint>(offsets);
	}

	public Pointer(Pointer pointer) {
		BaseAddress = pointer.BaseAddress;
		Offsets = new List<uint>(pointer.Offsets);
	}
}
