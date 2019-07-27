using System;
using System.Collections.Generic;
using static NativeSharp.NativeMethods;

namespace NativeSharp {
	/// <summary>
	/// 指针
	/// </summary>
	internal unsafe sealed class Pointer {
		private void* _baseAddress;
		private uint _baseOffset;
		private readonly List<uint> _offsets;

		/// <summary>
		/// 基址
		/// </summary>
		public void* BaseAddress {
			get => _baseAddress;
			set => _baseAddress = value;
		}

		/// <summary>
		/// 基址偏移
		/// </summary>
		public uint BaseOffset {
			get => _baseOffset;
			set => _baseOffset = value;
		}

		/// <summary>
		/// 多级偏移
		/// </summary>
		public IList<uint> Offsets => _offsets;

		/// <summary>
		/// 构造器
		/// </summary>
		/// <param name="baseAddress">基址</param>
		/// <param name="baseOffset">基址偏移</param>
		/// <param name="offsets">偏移</param>
		public Pointer(void* baseAddress, uint baseOffset, params uint[] offsets) {
			_baseAddress = baseAddress;
			_baseOffset = baseOffset;
			_offsets = new List<uint>(offsets);
		}

		/// <summary>
		/// 构造器
		/// </summary>
		/// <param name="pointer">指针</param>
		public Pointer(Pointer pointer) {
			_baseAddress = pointer._baseAddress;
			_baseOffset = pointer.BaseOffset;
			_offsets = new List<uint>(pointer._offsets);
		}
	}

	unsafe partial class NativeProcess {
		/// <summary>
		/// 获取指针指向的地址
		/// </summary>
		/// <param name="pointer">指针</param>
		/// <param name="address"></param>
		/// <returns></returns>
		public bool TryToAddress(Pointer pointer, out void* address) {
			address = default;
			if (pointer is null)
				return false;

			return ToAddressInternal(_handle, pointer, out address);
		}

		/// <summary>
		/// 读取内存
		/// </summary>
		/// <param name="address">地址</param>
		/// <param name="value">值</param>
		/// <returns></returns>
		public bool TryReadUInt32(void* address, out uint value) {
			return ReadUInt32Internal(_handle, address, out value);
		}

		/// <summary>
		/// 读取内存
		/// </summary>
		/// <param name="address">地址</param>
		/// <param name="value">值</param>
		/// <returns></returns>
		public bool TryReadIntPtr(void* address, out IntPtr value) {
			return ReadIntPtrInternal(_handle, address, out value);
		}

		internal static bool ToAddressInternal(void* processHandle, Pointer pointer, out void* address) {
			return IntPtr.Size == 8 ? ToAddressPrivate64(processHandle, pointer, out address) : ToAddressPrivate32(processHandle, pointer, out address);
		}

		private static bool ToAddressPrivate32(void* processHandle, Pointer pointer, out void* address) {
			uint newAddress;
			IList<uint> offsets;

			address = default;
			if (!ReadUInt32Internal(processHandle, (byte*)pointer.BaseAddress + pointer.BaseOffset, out newAddress))
				return false;
			offsets = pointer.Offsets;
			if (offsets.Count > 0) {
				for (int i = 0; i < offsets.Count - 1; i++) {
					newAddress += offsets[i];
					if (!ReadUInt32Internal(processHandle, (void*)newAddress, out newAddress))
						return false;
				}
				newAddress += offsets[offsets.Count - 1];
			}
			address = (void*)newAddress;
			return true;
		}

		private static bool ToAddressPrivate64(void* processHandle, Pointer pointer, out void* address) {
			ulong newAddress;
			IList<uint> offsets;

			address = default;
			if (!ReadUInt64Internal(processHandle, (byte*)pointer.BaseAddress + pointer.BaseOffset, out newAddress))
				return false;
			offsets = pointer.Offsets;
			if (offsets.Count > 0) {
				for (int i = 0; i < offsets.Count - 1; i++) {
					newAddress += offsets[i];
					if (!ReadUInt64Internal(processHandle, (void*)newAddress, out newAddress))
						return false;
				}
				newAddress += offsets[offsets.Count - 1];
			}
			address = (void*)newAddress;
			return true;
		}

		internal static bool ReadUInt32Internal(void* processHandle, void* address, out uint value) {
			fixed (void* p = &value)
				return ReadInternal(processHandle, address, p, 4);
		}

		internal static bool ReadUInt64Internal(void* processHandle, void* address, out ulong value) {
			fixed (void* p = &value)
				return ReadInternal(processHandle, address, p, 8);
		}

		internal static bool ReadIntPtrInternal(void* processHandle, void* address, out IntPtr value) {
			fixed (void* p = &value)
				return ReadInternal(processHandle, address, p, (uint)IntPtr.Size);
		}

		internal static bool ReadBytesInternal(void* processHandle, void* address, byte[] value, uint startIndex, uint length) {
			fixed (void* p = &value[startIndex])
				return ReadInternal(processHandle, address, p, length);
		}

		internal static bool ReadInternal(void* processHandle, void* address, void* value, uint length) {
			return ReadProcessMemory(processHandle, address, value, length, null);
		}
	}
}
