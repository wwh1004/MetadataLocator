using System;
using static MetadataLocator.NativeSharp.NativeMethods;

namespace MetadataLocator.NativeSharp;

static unsafe class NativeProcess {
	static readonly nuint handle = GetCurrentProcess();

	public static bool TryToAddress(Pointer pointer, out nuint address) {
		address = default;
		if (pointer is null)
			return false;

		return ToAddressInternal(handle, pointer, out address);
	}

	public static bool TryReadUInt32(nuint address, out uint value) {
		return ReadUInt32Internal(handle, address, out value);
	}

	public static bool TryReadIntPtr(nuint address, out nuint value) {
		return ReadIntPtrInternal(handle, address, out value);
	}

	static bool ToAddressInternal(nuint processHandle, Pointer pointer, out nuint address) {
		return sizeof(nuint) == 8 ? ToAddressPrivate64(processHandle, pointer, out address) : ToAddressPrivate32(processHandle, pointer, out address);
	}

	static bool ToAddressPrivate32(nuint processHandle, Pointer pointer, out nuint address) {
		address = default;
		uint newAddress = (uint)pointer.BaseAddress;
		var offsets = pointer.Offsets;
		if (offsets.Count > 0) {
			for (int i = 0; i < offsets.Count - 1; i++) {
				newAddress += offsets[i];
				if (!ReadUInt32Internal(processHandle, newAddress, out newAddress))
					return false;
			}
			newAddress += offsets[offsets.Count - 1];
		}
		address = newAddress;
		return true;
	}

	static bool ToAddressPrivate64(nuint processHandle, Pointer pointer, out nuint address) {
		address = default;
		ulong newAddress = pointer.BaseAddress;
		var offsets = pointer.Offsets;
		if (offsets.Count > 0) {
			for (int i = 0; i < offsets.Count - 1; i++) {
				newAddress += offsets[i];
				if (!ReadUInt64Internal(processHandle, (nuint)newAddress, out newAddress))
					return false;
			}
			newAddress += offsets[offsets.Count - 1];
		}
		address = (nuint)newAddress;
		return true;
	}

	static bool ReadUInt32Internal(nuint processHandle, nuint address, out uint value) {
		fixed (void* p = &value)
			return ReadInternal(processHandle, address, (nuint)p, 4);
	}

	static bool ReadUInt64Internal(nuint processHandle, nuint address, out ulong value) {
		fixed (void* p = &value)
			return ReadInternal(processHandle, address, (nuint)p, 8);
	}

	static bool ReadIntPtrInternal(nuint processHandle, nuint address, out nuint value) {
		fixed (void* p = &value)
			return ReadInternal(processHandle, address, (nuint)p, (uint)sizeof(nuint));
	}

	static bool ReadInternal(nuint processHandle, nuint address, nuint value, uint length) {
		return ReadProcessMemory(processHandle, address, value, length, out _);
	}

	public static nuint GetModule(string moduleName) {
		if (string.IsNullOrEmpty(moduleName))
			throw new ArgumentNullException(nameof(moduleName));

		return GetModuleHandle(moduleName);
	}
}
