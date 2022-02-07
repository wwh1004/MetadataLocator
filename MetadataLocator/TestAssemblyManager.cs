using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace MetadataLocator;

/// <summary>
/// Test assembly generation options
/// </summary>
[Flags]
enum TestAssemblyFlags {
	/// <summary>
	/// The number of assembly
	/// </summary>
	IndexMask = 0xFF,

	/// <summary>
	/// Load it from memory
	/// </summary>
	InMemory = 0x100,

	/// <summary>
	/// Use #- table stream
	/// </summary>
	Uncompressed = 0x200
}

/// <summary>
/// Assembly for <see cref="MetadataInfoImpl"/> test
/// </summary>
sealed class TestAssembly {
	public TestAssemblyFlags Flags { get; }

	public int Index => (int)(Flags & TestAssemblyFlags.IndexMask);

	public bool InMemory => (Flags & TestAssemblyFlags.InMemory) != 0;

	public bool Uncompressed => (Flags & TestAssemblyFlags.Uncompressed) != 0;

	public Assembly Assembly { get; }

	public Module Module { get; }

	public nuint ModuleHandle { get; }

	public TestAssembly(TestAssemblyFlags flags, Assembly assembly) {
		Flags = flags;
		Assembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
		Module = assembly.ManifestModule;
		ModuleHandle = ReflectionHelpers.GetModuleHandle(assembly.ManifestModule);
	}
}

/// <summary>
/// Manager of <see cref="TestAssembly"/>
/// </summary>
sealed class TestAssemblyManager {
	const int CACHE_MAGIC = 1;
	// Update it if old cache format is invalid

	static readonly Dictionary<TestAssemblyFlags, TestAssembly> assemblies = new();

	/// <summary>
	/// Get a test assembly
	/// </summary>
	/// <param name="flags"></param>
	/// <returns></returns>
	public static TestAssembly GetAssembly(TestAssemblyFlags flags) {
		if (!assemblies.TryGetValue(flags, out var testAssembly)) {
			string path = GetOrCreateAssembly(flags);
			bool inMemory = (flags & TestAssemblyFlags.InMemory) != 0;
			var assembly = inMemory ? Assembly.Load(File.ReadAllBytes(path)) : Assembly.LoadFile(path);
			testAssembly = new TestAssembly(flags, assembly);
			assemblies.Add(flags, testAssembly);
		}
		return testAssembly;
	}

	static string GetOrCreateAssembly(TestAssemblyFlags flags) {
		var tempDirectory = Path.Combine(Path.GetTempPath(), $"MetadataLocator_{CACHE_MAGIC:X2}_{RuntimeEnvironment.Version}");
		if (!Directory.Exists(tempDirectory))
			Directory.CreateDirectory(tempDirectory);
		string outputPath = Path.Combine(tempDirectory, $"{(uint)flags:X8}.dll");

		if (File.Exists(outputPath))
			return outputPath;

		using var provider = CodeDomProvider.CreateProvider("cs");
		var options = new CompilerParameters {
			GenerateExecutable = false,
			OutputAssembly = outputPath
		};
		var assembly = new CodeCompileUnit();
		var @namespace = new CodeNamespace("ns1");
		assembly.Namespaces.Add(@namespace);
		// write namespace
		var @class = new CodeTypeDeclaration("class1");
		@namespace.Types.Add(@class);
		// write class
		@class.Members.Add(new CodeMemberMethod() {
			Name = "method1"
		});
		// write method
		var results = provider.CompileAssemblyFromDom(options, assembly);
		if ((flags & TestAssemblyFlags.Uncompressed) != 0) {
			byte[] data = File.ReadAllBytes(outputPath);
			ConvertToUncompressed(data);
			File.WriteAllBytes(outputPath, data);
		}
		return outputPath;
	}

	static void ConvertToUncompressed(byte[] data) {
		int i = 0;
		for (; i < data.Length; i++) {
			if (data[i] == 'B' && data[i + 1] == 'S' && data[i + 2] == 'J' && data[i + 3] == 'B')
				break;
		}
		Debug2.Assert(i != data.Length);
		for (; i < data.Length; i++) {
			if (data[i] == '#' && data[i + 1] == '~')
				break;
		}
		Debug2.Assert(i != data.Length);
		data[i + 1] = (byte)'-';
	}
}
