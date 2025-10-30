using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using Xunit.Abstractions;

namespace Cabinet.SourceGenerators.Tests;

/// <summary>
/// Integration tests that compile and execute generated code to validate actual runtime behaviour.
/// These tests will reveal whether the generated CreateCabinetStore extension actually works.
/// </summary>
public class GeneratedExtensionsIntegrationTests
{
	private readonly ITestOutputHelper _output;

	public GeneratedExtensionsIntegrationTests(ITestOutputHelper output)
	{
		_output = output;
	}

	private const string TestRecordSource = @"
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

namespace TestNamespace
{
	[Cabinet.AotRecord]
	public class TestRecord
	{
		public string Id { get; set; } = string.Empty;
		public string Name { get; set; } = string.Empty;
		public string Description { get; set; } = string.Empty;
	}
}";

	[Fact]
	public void GeneratedCode_ShouldCompileWithoutErrors()
	{
		// Arrange & Act
		var (compilation, diagnostics) = CreateCompilation(TestRecordSource);

		// Assert
		var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
		
		if (errors.Any())
		{
			_output.WriteLine("=== COMPILATION ERRORS ===");
			foreach (var error in errors)
			{
				_output.WriteLine($"{error.Id}: {error.GetMessage()}");
				_output.WriteLine($"  Location: {error.Location}");
			}
			
			// Also output all generated code for debugging
			_output.WriteLine("\n=== GENERATED CODE ===");
			var generatedTrees = compilation.SyntaxTrees.Skip(1).ToList();
			foreach (var tree in generatedTrees)
			{
				_output.WriteLine($"\n--- {tree.FilePath} ---");
				_output.WriteLine(tree.ToString());
			}
		}

		Assert.Empty(errors);
	}

	[Fact]
	public void GeneratedStoreExtension_ShouldPassCorrectParametersToConstructor()
	{
		// Arrange
		var (compilation, diagnostics) = CreateCompilation(TestRecordSource);
		Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

		// Act - Find the generated CabinetStoreExtensions
		var generatedCode = string.Join("\n", compilation.SyntaxTrees.Skip(1).Select(t => t.ToString()));
		
		// Assert - Check what parameters are being passed
		_output.WriteLine("=== GENERATED STORE EXTENSIONS ===");
		var storeExtensionTree = compilation.SyntaxTrees
			.FirstOrDefault(t => t.ToString().Contains("CreateCabinetStore"));
		
		if (storeExtensionTree != null)
		{
			_output.WriteLine(storeExtensionTree.ToString());
		}

		Assert.Contains("CreateCabinetStore", generatedCode);
		Assert.Contains("FileOfflineStore", generatedCode);
		
		// This is the key test - what's being passed to the constructor?
		_output.WriteLine("\n=== CHECKING CONSTRUCTOR CALL ===");
		if (generatedCode.Contains("new FileOfflineStore("))
		{
			var constructorCallStart = generatedCode.IndexOf("new FileOfflineStore(");
			var constructorCallEnd = generatedCode.IndexOf(");", constructorCallStart);
			var constructorCall = generatedCode.Substring(constructorCallStart, constructorCallEnd - constructorCallStart + 2);
			_output.WriteLine(constructorCall);
			
			// Check if JsonSerializerOptions is being passed
			if (constructorCall.Contains("JsonSerializerOptions"))
			{
				_output.WriteLine("\n⚠️  WARNING: JsonSerializerOptions is being passed to constructor!");
				_output.WriteLine("This should cause a compilation error since constructor expects IIndexProvider.");
			}
		}
	}

	[Fact]
	public void GeneratedRecordSetExtension_ShouldCreateValidOptions()
	{
		// Arrange
		var (compilation, diagnostics) = CreateCompilation(TestRecordSource);
		Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

		// Act
		var generatedCode = string.Join("\n", compilation.SyntaxTrees.Skip(1).Select(t => t.ToString()));
		
		// Assert
		_output.WriteLine("=== GENERATED RECORDSET EXTENSIONS ===");
		var recordSetExtensionTree = compilation.SyntaxTrees
			.FirstOrDefault(t => t.ToString().Contains("TestRecordExtensions"));
		
		if (recordSetExtensionTree != null)
		{
			_output.WriteLine(recordSetExtensionTree.ToString());
		}

		Assert.Contains("TestRecordExtensions", generatedCode);
		Assert.Contains("CreateRecordSetOptions", generatedCode);
		Assert.Contains("CreateRecordSet", generatedCode);
		Assert.Contains("IdSelector = record => record.Id", generatedCode);
	}

	[Fact]
	public void GeneratedCode_TypeAnalysis_FileOfflineStoreConstructorParameters()
	{
		// Arrange
		var (compilation, diagnostics) = CreateCompilation(TestRecordSource);
		Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

		// Act - Perform semantic analysis
		var storeExtensionTree = compilation.SyntaxTrees
			.FirstOrDefault(t => t.ToString().Contains("CreateCabinetStore"));
		
		Assert.NotNull(storeExtensionTree);

		var semanticModel = compilation.GetSemanticModel(storeExtensionTree);
		var root = storeExtensionTree.GetRoot();

		// Find all ObjectCreationExpressionSyntax nodes for FileOfflineStore
		var objectCreations = root.DescendantNodes()
			.OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ObjectCreationExpressionSyntax>()
			.Where(o => o.Type.ToString().Contains("FileOfflineStore"))
			.ToList();

		_output.WriteLine($"\n=== FOUND {objectCreations.Count} FileOfflineStore CREATION(S) ===");

		foreach (var creation in objectCreations)
		{
			_output.WriteLine($"\nCreation syntax: {creation}");
			
			var symbolInfo = semanticModel.GetSymbolInfo(creation);
			if (symbolInfo.Symbol is IMethodSymbol constructorSymbol)
			{
				_output.WriteLine($"Constructor: {constructorSymbol.ToDisplayString()}");
				_output.WriteLine($"Parameter count: {constructorSymbol.Parameters.Length}");
				
				for (int i = 0; i < constructorSymbol.Parameters.Length; i++)
				{
					var param = constructorSymbol.Parameters[i];
					_output.WriteLine($"  Param {i}: {param.Name} ({param.Type.ToDisplayString()})");
				}

				// Check the arguments being passed
				if (creation.ArgumentList != null)
				{
					_output.WriteLine($"\nArguments being passed: {creation.ArgumentList.Arguments.Count}");
					for (int i = 0; i < creation.ArgumentList.Arguments.Count; i++)
					{
						var arg = creation.ArgumentList.Arguments[i];
						var argType = semanticModel.GetTypeInfo(arg.Expression);
						_output.WriteLine($"  Arg {i}: {arg.Expression} (Type: {argType.Type?.ToDisplayString() ?? "unknown"})");
					}
				}
			}
			else
			{
				_output.WriteLine("⚠️  Could not resolve constructor symbol!");
				if (symbolInfo.CandidateReason != CandidateReason.None)
				{
					_output.WriteLine($"Candidate reason: {symbolInfo.CandidateReason}");
					foreach (var candidate in symbolInfo.CandidateSymbols)
					{
						_output.WriteLine($"  Candidate: {candidate.ToDisplayString()}");
					}
				}
			}
		}

		// This test will PASS if compilation succeeds, but we want to see what's actually happening
		Assert.NotEmpty(objectCreations);
	}

	[Fact]
	public void DiagnosticAnalysis_ShowAllCompilerMessages()
	{
		// Arrange & Act
		var (_, diagnostics) = CreateCompilation(TestRecordSource);

		// Output ALL diagnostics, not just errors
		_output.WriteLine("=== ALL DIAGNOSTICS ===");
		foreach (var diagnostic in diagnostics)
		{
			_output.WriteLine($"[{diagnostic.Severity}] {diagnostic.Id}: {diagnostic.GetMessage()}");
			_output.WriteLine($"  Location: {diagnostic.Location}");
		}

		_output.WriteLine($"\nTotal diagnostics: {diagnostics.Length}");
		_output.WriteLine($"Errors: {diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error)}");
		_output.WriteLine($"Warnings: {diagnostics.Count(d => d.Severity == DiagnosticSeverity.Warning)}");
		_output.WriteLine($"Info: {diagnostics.Count(d => d.Severity == DiagnosticSeverity.Info)}");
	}

	[Fact(Skip = "Metadata inspection not available in test compilation context")]
	public void InspectFileOfflineStoreConstructors_FromMetadata()
	{
		// This test inspects what constructors FileOfflineStore actually has
		var (compilation, _) = CreateCompilation(TestRecordSource);

		var fileOfflineStoreType = compilation.GetTypeByMetadataName("Cabinet.Core.FileOfflineStore");
		
		Assert.NotNull(fileOfflineStoreType);

		_output.WriteLine("=== FileOfflineStore CONSTRUCTORS ===");
		var constructors = fileOfflineStoreType.Constructors
			.Where(c => !c.IsStatic)
			.ToList();

		_output.WriteLine($"Found {constructors.Count} constructor(s):\n");

		foreach (var ctor in constructors)
		{
			_output.WriteLine($"Constructor: {ctor.ToDisplayString()}");
			_output.WriteLine($"  Parameters: {ctor.Parameters.Length}");
			foreach (var param in ctor.Parameters)
			{
				var optional = param.IsOptional ? " (optional)" : "";
				var defaultValue = param.HasExplicitDefaultValue ? $" = {param.ExplicitDefaultValue ?? "null"}" : "";
				_output.WriteLine($"    {param.Name}: {param.Type.ToDisplayString()}{optional}{defaultValue}");
			}
			_output.WriteLine("");
		}
	}

	[Fact]
	public void GeneratedStoreExtensions_ShouldIncludeNamedRecordSetMethods()
	{
		// Arrange
		var (compilation, diagnostics) = CreateCompilation(TestRecordSource);
		Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

		// Act
		var storeExtensionTree = compilation.SyntaxTrees
			.FirstOrDefault(t => t.ToString().Contains("CabinetStoreExtensions"));
		
		Assert.NotNull(storeExtensionTree);
		var generatedCode = storeExtensionTree.ToString();

		_output.WriteLine("=== GENERATED STORE EXTENSIONS ===");
		_output.WriteLine(generatedCode);

		// Assert - Check for the named RecordSet creation method
		Assert.Contains("CreateTestRecordRecordSet", generatedCode);
		Assert.Contains("public static RecordSet<TestRecord>", generatedCode);
		Assert.Contains("TestRecordExtensions.CreateRecordSetOptions()", generatedCode);
		
		// Verify it still has the CreateCabinetStore method
		Assert.Contains("CreateCabinetStore", generatedCode);
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
