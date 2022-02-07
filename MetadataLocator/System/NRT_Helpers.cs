using System.Diagnostics.CodeAnalysis;

namespace System {
	static class string2 {
		public static bool IsNullOrEmpty([NotNullWhen(false)] string? value) {
			return string.IsNullOrEmpty(value);
		}
	}
}

namespace System.Diagnostics {
	static class Debug2 {
		[Conditional("DEBUG")]
		public static void Assert([DoesNotReturnIf(false)] bool condition) {
			Debug.Assert(condition);
		}

		[Conditional("DEBUG")]
		public static void Assert([DoesNotReturnIf(false)] bool condition, string? message) {
			Debug.Assert(condition, message);
		}
	}
}
