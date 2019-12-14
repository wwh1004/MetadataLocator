using System.Collections.Generic;

namespace MetadataLocator.NativeSharp {
	internal unsafe sealed class Pointer {
		private void* _baseAddress;
		private readonly List<uint> _offsets;

		public void* BaseAddress {
			get => _baseAddress;
			set => _baseAddress = value;
		}

		public IList<uint> Offsets => _offsets;

		public Pointer(void* baseAddress, params uint[] offsets) {
			_baseAddress = baseAddress;
			_offsets = new List<uint>(offsets);
		}

		public Pointer(Pointer pointer) {
			_baseAddress = pointer._baseAddress;
			_offsets = new List<uint>(pointer._offsets);
		}
	}
}
