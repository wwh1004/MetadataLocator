using System;

namespace NativeSharp {
	unsafe partial class NativeProcess {
		/// <summary>
		/// 获取模块
		/// </summary>
		/// <param name="moduleName">模块名</param>
		/// <returns></returns>
		public NativeModule GetModule(string moduleName) {
			if (string.IsNullOrEmpty(moduleName))
				throw new ArgumentNullException(nameof(moduleName));

			void* moduleHandle;

			moduleHandle = GetModuleHandleInternal(_handle, false, moduleName);
			return moduleHandle is null ? null : new NativeModule();
		}
	}
}
