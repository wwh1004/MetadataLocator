using System.Diagnostics;

namespace MetadataLocator;

static unsafe class LdasmExtensions {
	/// <summary>
	/// Try get <paramref name="displacement"/> from instruction
	/// </summary>
	/// <param name="ldasm"></param>
	/// <param name="address">Instruction address</param>
	/// <param name="displacement"></param>
	/// <returns></returns>
	public static bool TryGetDisplacement(this in Ldasm ldasm, nuint address, out uint displacement) {
		displacement = 0;
		if ((ldasm.flags & Ldasm.F_DISP) == 0)
			return false;

		switch (ldasm.disp_size) {
		case 1:
			displacement = *(byte*)(address + ldasm.disp_offset);
			return true;
		case 2:
			displacement = *(ushort*)(address + ldasm.disp_offset);
			return true;
		case 4:
			displacement = *(uint*)(address + ldasm.disp_offset);
			return true;
		default:
			Debug2.Assert(false);
			return false;
		}
	}

	/// <summary>
	/// Try get <paramref name="immediate"/> from instruction
	/// </summary>
	/// <param name="ldasm"></param>
	/// <param name="address">Instruction address</param>
	/// <param name="immediate"></param>
	/// <returns></returns>
	public static bool TryGetImmediate(this in Ldasm ldasm, nuint address, out ulong immediate) {
		immediate = 0;
		if ((ldasm.flags & Ldasm.F_IMM) == 0)
			return false;

		switch (ldasm.imm_size) {
		case 1:
			immediate = *(byte*)(address + ldasm.imm_offset);
			return true;
		case 2:
			immediate = *(ushort*)(address + ldasm.imm_offset);
			return true;
		case 4:
			immediate = *(uint*)(address + ldasm.imm_offset);
			return true;
		case 8:
			immediate = *(ulong*)(address + ldasm.imm_offset);
			return true;
		default:
			Debug2.Assert(false);
			return false;
		}
	}
}
