using System;
using static NativeSharp.NativeMethods;

namespace NativeSharp {
	/// <summary>
	/// Win32进程
	/// </summary>
	internal sealed unsafe partial class NativeProcess : IDisposable {
		private static readonly NativeProcess _currentProcess = new NativeProcess(GetCurrentProcess());

		private readonly void* _handle;

		/// <summary>
		/// 当前进程
		/// </summary>
		public static NativeProcess CurrentProcess => _currentProcess;

		private NativeProcess(void* handle) {
			_handle = handle;
		}

		/// <summary />
		public void Dispose() {
		}
	}
}
