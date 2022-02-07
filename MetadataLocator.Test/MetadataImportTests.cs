namespace MetadataLocator.Test;

static unsafe class MetadataImportTests {
	public static void Test() {
		var module = typeof(MetadataImportTests).Module;
		var metadataImport = MetadataImport.Create(module);
		Assert.IsTrue(metadataImport is not null);
		nuint pVersion = metadataImport.GetVersionString();
		var version = new string((sbyte*)pVersion);
		Assert.IsTrue(version == "v2.0.50727");
	}
}
