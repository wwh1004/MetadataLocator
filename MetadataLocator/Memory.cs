using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;

namespace MetadataLocator;

sealed class Pointer {
	public static readonly Pointer Empty = new(0, Array2.Empty<uint>());

	public bool IsEmpty => BaseAddress == 0 && Offsets.Count == 0;

	public nuint BaseAddress { get; set; }

	public IList<uint> Offsets { get; }

	public Pointer(IEnumerable<uint> offsets) {
		Offsets = new List<uint>(offsets);
	}

	public Pointer(nuint baseAddress, IEnumerable<uint> offsets) {
		BaseAddress = baseAddress;
		Offsets = new List<uint>(offsets);
	}

	public Pointer(Pointer pointer) {
		BaseAddress = pointer.BaseAddress;
		Offsets = new List<uint>(pointer.Offsets);
	}

	public void Add(uint offset) {
		Offsets.Add(offset);
	}

	public void Add(IEnumerable<uint> offsets) {
		if (offsets is null)
			throw new ArgumentNullException(nameof(offsets));

		foreach (uint offset in offsets)
			Offsets.Add(offset);
	}
}

static unsafe class Memory {
	[ThreadStatic]
	static readonly byte[] stringBuffer = new byte[4096];

	[HandleProcessCorruptedStateExceptions]
	public static bool TryToAddress(Pointer pointer, out nuint address) {
		address = 0;
		if (pointer is null)
			return false;

		try {
			address = 0;
			nuint newAddress = pointer.BaseAddress;
			var offsets = pointer.Offsets;
			if (offsets.Count > 0) {
				for (int i = 0; i < offsets.Count - 1; i++) {
					newAddress += offsets[i];
					if (!TryReadUIntPtr(newAddress, out newAddress))
						return false;
				}
				newAddress += offsets[offsets.Count - 1];
			}
			address = newAddress;
			return true;
		}
		catch {
			return false;
		}
	}

	[HandleProcessCorruptedStateExceptions]
	public static bool TryReadUInt32(nuint address, out uint value) {
		value = 0;
		if (address == 0)
			return false;

		if (IsBadReadPtr(address, 4))
			return false;

		try {
			value = *(uint*)address;
			return true;
		}
		catch {
			return false;
		}
	}

	[HandleProcessCorruptedStateExceptions]
	public static bool TryReadUInt64(nuint address, out ulong value) {
		value = 0;
		if (address == 0)
			return false;

		if (IsBadReadPtr(address, 8))
			return false;

		try {
			value = *(ulong*)address;
			return true;
		}
		catch {
			return false;
		}
	}

	[HandleProcessCorruptedStateExceptions]
	public static bool TryReadUIntPtr(nuint address, out nuint value) {
		value = 0;
		if (address == 0)
			return false;

		if (IsBadReadPtr(address, (uint)sizeof(nuint)))
			return false;

		try {
			value = *(nuint*)address;
			return true;
		}
		catch {
			value = 0;
			return false;
		}
	}

	[HandleProcessCorruptedStateExceptions]
	public static bool TryReadAnsiString(nuint address, [NotNullWhen(true)] out string? value) {
		value = null;
		if (address == 0)
			return false;

		if (IsBadReadPtr(address, 8))
			return false;

		try {
			value = new string((sbyte*)address);
			return true;
		}
		catch {
			return false;
		}
	}

	[HandleProcessCorruptedStateExceptions]
	public static bool TryReadUnicodeString(nuint address, [NotNullWhen(true)] out string? value) {
		value = null;
		if (address == 0)
			return false;

		if (IsBadReadPtr(address, 8))
			return false;

		try {
			value = new string((char*)address);
			return true;
		}
		catch {
			return false;
		}
	}

	[HandleProcessCorruptedStateExceptions]
	public static bool TryReadUtf8String(nuint address, [NotNullWhen(true)] out string? value) {
		value = null;
		if (address == 0)
			return false;

		if (IsBadReadPtr(address, 8))
			return false;

		try {
			uint i = 0;
			for (; *(byte*)(address + i) != 0; i++)
				stringBuffer[i] = *(byte*)(address + i);
			// TODO: assume no string larger than 4096 bytes
			stringBuffer[i] = 0;
			value = Encoding.UTF8.GetString(stringBuffer, 0, (int)i);
			return true;
		}
		catch {
			return false;
		}
	}

	[DllImport("kernel32.dll", SetLastError = true)]
	static extern bool IsBadReadPtr(nuint lp, nuint ucb);
	// TODO: In .NET Core, HandleProcessCorruptedStateExceptionsAttribute is invalid and we can't capture AccessViolationException no longer.
	// We should find a better way to check address readable.
}
