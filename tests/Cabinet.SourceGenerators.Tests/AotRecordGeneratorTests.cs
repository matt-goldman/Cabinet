using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Cabinet.SourceGenerators.Tests;

public class AotRecordGeneratorTests
{
	private const string AttributeSource = @"
using System;

namespace Cabinet
{
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
	public class AotRecordAttribute : Attribute
	{
		public string? IdPropertyName { get; set; }
		public string? FileName { get; set; }
	}
}
";

	[Fact]
	public void GeneratesCodeForClassWithIdProperty()
	{
		// Arrange
		string source = AttributeSource + @"
namespace TestNamespace
{
	[Cabinet.AotRecord]
	public class TestRecord
	{
		public string Id { get; set; } = string.Empty;
		public string Name { get; set; } = string.Empty;
	}
}";

		// Act
		var (compilation, diagnostics) = CreateCompilation(source);

		// Assert - check for errors
		var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
		if (errors.Any())
		{
			var errorMessages = string.Join("\n", errors.Select(e => e.GetMessage()));
			Assert.Fail($"Compilation errors:\n{errorMessages}");
		}

		// Verify generated files
		var generatedTrees = compilation.SyntaxTrees.Skip(1).ToList();
		if (!generatedTrees.Any())
		{
			// Debug: Show all syntax trees
			var allTrees = string.Join("\n---\n", compilation.SyntaxTrees.Select(t => $"File: {t.FilePath}\n{t}"));
			Assert.Fail($"No generated code found. Total trees: {compilation.SyntaxTrees.Count()}\n{allTrees}");
		}

		// Check that the generated code contains expected elements
		var allGeneratedCode = string.Join("\n", generatedTrees.Select(t => t.ToString()));
		Assert.Contains("CabinetJsonSerializerContext", allGeneratedCode);
		Assert.Contains("TestRecordExtensions", allGeneratedCode);
		Assert.Contains("CabinetStoreExtensions", allGeneratedCode);
		Assert.Contains("record => record.Id", allGeneratedCode);
	}

	[Fact]
	public void GeneratesCodeForClassWithTypedIdProperty()
	{
		// Arrange
		string source = AttributeSource + @"
namespace TestNamespace
{
	[Cabinet.AotRecord]
	public class LessonRecord
	{
		public string LessonRecordId { get; set; } = string.Empty;
		public string Title { get; set; } = string.Empty;
	}
}";

		// Act
		var (compilation, diagnostics) = CreateCompilation(source);

		// Assert
		Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

		var allGeneratedCode = string.Join("\n", compilation.SyntaxTrees.Skip(1).Select(t => t.ToString()));
		if (string.IsNullOrEmpty(allGeneratedCode))
		{
			Assert.Fail("No generated code was produced");
		}
		Assert.Contains("record => record.LessonRecordId", allGeneratedCode);
	}

	[Fact]
	public void GeneratesCodeForClassWithExplicitIdPropertyName()
	{
		// Arrange
		string source = AttributeSource + @"
namespace TestNamespace
{
	[Cabinet.AotRecord(IdPropertyName = ""CustomId"")]
	public class CustomRecord
	{
		public string CustomId { get; set; } = string.Empty;
		public string Data { get; set; } = string.Empty;
	}
}";

		// Act
		var (compilation, diagnostics) = CreateCompilation(source);

		// Assert
		Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

		var allGeneratedCode = string.Join("\n", compilation.SyntaxTrees.Skip(1).Select(t => t.ToString()));
		Assert.Contains("record => record.CustomId", allGeneratedCode);
	}

	[Fact]
	public void GeneratesCodeForMultipleRecordTypes()
	{
		// Arrange
		string source = AttributeSource + @"
namespace TestNamespace
{
	[Cabinet.AotRecord]
	public class FirstRecord
	{
		public string Id { get; set; } = string.Empty;
	}

	[Cabinet.AotRecord]
	public class SecondRecord
	{
		public string SecondRecordId { get; set; } = string.Empty;
	}
}";

		// Act
		var (compilation, diagnostics) = CreateCompilation(source);

		// Assert
		Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

		var allGeneratedCode = string.Join("\n", compilation.SyntaxTrees.Skip(1).Select(t => t.ToString()));
		
		// Both types should be in JSON context
		Assert.Contains("JsonSerializable(typeof(FirstRecord))", allGeneratedCode);
		Assert.Contains("JsonSerializable(typeof(SecondRecord))", allGeneratedCode);
		Assert.Contains("JsonSerializable(typeof(List<FirstRecord>))", allGeneratedCode);
		Assert.Contains("JsonSerializable(typeof(List<SecondRecord>))", allGeneratedCode);

		// Both should have extensions
		Assert.Contains("FirstRecordExtensions", allGeneratedCode);
		Assert.Contains("SecondRecordExtensions", allGeneratedCode);
	}

	private (Compilation, ImmutableArray<Diagnostic>) CreateCompilation(string source)
	{
		var syntaxTree = CSharpSyntaxTree.ParseText(source);

		var references = new List<MetadataReference>();
		
		// Add all assemblies that the source references
		var assemblies = AppDomain.CurrentDomain.GetAssemblies();
		foreach (var assembly in assemblies)
		{
			if (!assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location))
			{
				references.Add(MetadataReference.CreateFromFile(assembly.Location));
			}
		}

		var compilation = CSharpCompilation.Create(
			"TestCompilation",
			new[] { syntaxTree },
			references,
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

		// Create the generator
		var generator = new AotRecordGenerator();

		// Run the generator
		var driver = CSharpGeneratorDriver.Create(generator);
		driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(
			compilation, 
			out var outputCompilation, 
			out var diagnostics);

		return (outputCompilation, diagnostics);
	}
}
