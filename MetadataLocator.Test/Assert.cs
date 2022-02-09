using System;
using System.Diagnostics.CodeAnalysis;

namespace MetadataLocator.Test;

sealed class AssertFailedException : Exception {
	public AssertFailedException() {
	}

	public AssertFailedException(string message) : base(message) {
	}
}

static class Assert {
	public static void IsTrue([DoesNotReturnIf(false)] bool condition) {
		if (!condition)
			throw new AssertFailedException();
	}

	public static void IsTrue([DoesNotReturnIf(false)] bool condition, string message) {
		if (!condition)
			throw new AssertFailedException(message);
	}
}
