# Cabinet Roadmap

This document outlines planned features and improvements for Cabinet.

## Current Version

Cabinet provides a lightweight, encrypted offline data layer for .NET applications with:

- ✅ Encrypted storage with AES-256-GCM
- ✅ Full-text search with encrypted indexes
- ✅ `RecordSet<T>` high-level abstraction
- ✅ `RecordQuery<T>` LINQ-style querying
- ✅ `RecordCollection<T>` for scoped collections
- ✅ Cross-platform support (.NET MAUI, .NET 9+)
- ✅ AOT compatibility (with manual configuration)

---

## Phase 1: Source Generator for AOT (High Priority)

### Goal

Eliminate manual AOT configuration boilerplate and reduce developer friction.

### Problem

Currently, AOT scenarios require:

1. Manual `[JsonSerializable]` attributes for every record type AND `List<T>`
2. Manual `IdSelector` configuration in `RecordSetOptions<T>`
3. Easy to miss `List<T>` serialization registration
4. No compile-time validation of ID property existence

### Solution: `[AotRecord]` Attribute + Source Generator

#### Usage

```csharp
[AotRecord]
public class LessonRecord
{
    public string LessonId { get; set; }
    public string Title { get; set; }
    public DateTime Date { get; set; }
}

[AotRecord]
public class ChildRecord
{
    public string ChildId { get; set; }
    public string Name { get; set; }
    public int Age { get; set; }
}
```

#### Generated Outputs

##### 1. JSON Serialization Context

```csharp
// Auto-generated: CabinetJsonSerializerContext.g.cs
namespace Cabinet.Generated;

[JsonSerializable(typeof(LessonRecord))]
[JsonSerializable(typeof(List<LessonRecord>))]
[JsonSerializable(typeof(ChildRecord))]
[JsonSerializable(typeof(List<ChildRecord>))]
[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
public partial class CabinetJsonSerializerContext : JsonSerializerContext
{
}
```

##### 2. RecordSet Extensions

```csharp
// Auto-generated: LessonRecordExtensions.g.cs
namespace Cabinet.Generated;

public static class LessonRecordExtensions
{
    public static RecordSetOptions<LessonRecord> CreateRecordSetOptions()
        => new()
        {
            IdSelector = record => record.LessonId
        };
    
    public static RecordSet<LessonRecord> CreateRecordSet(this IOfflineStore store)
        => new(store, CreateRecordSetOptions());
}
```

##### 3. Store Extensions

```csharp
// Auto-generated: CabinetStoreExtensions.g.cs
namespace Cabinet.Generated;

public static class CabinetStoreExtensions
{
    public static IOfflineStore CreateCabinetStore(
        string dataDirectory,
        byte[] masterKey)
    {
        var encryptionProvider = new AesGcmEncryptionProvider(masterKey);
        return new FileOfflineStore(
            dataDirectory,
            encryptionProvider,
            new JsonSerializerOptions
            {
                TypeInfoResolver = CabinetJsonSerializerContext.Default
            });
    }
}
```

#### Final Developer Experience

```csharp
// 1. Decorate your records
[AotRecord]
public class LessonRecord
{
    public string LessonId { get; set; }
    // ... properties
}

// 2. Create store (using generated helper)
var store = CabinetStoreExtensions.CreateCabinetStore(
    FileSystem.AppDataDirectory,
    masterKey);

// 3. Create RecordSet (using generated helper)
var lessons = store.CreateRecordSet<LessonRecord>();

// 4. Use it!
await lessons.LoadAsync();
await lessons.AddAsync(newLesson);
```

#### Implementation Details

**Project Structure:**

```tree
Cabinet.SourceGenerators/
├── Cabinet.SourceGenerators.csproj
├── AotRecordAttribute.cs          // The [AotRecord] attribute
├── AotRecordGenerator.cs          // Main source generator
├── Analyzers/
│   └── AotRecordAnalyzer.cs       // Compile-time validation
└── Templates/
    ├── JsonContextTemplate.cs
    ├── RecordSetExtensionsTemplate.cs
    └── StoreExtensionsTemplate.cs
```

**Attribute Definition:**

```csharp
namespace Cabinet;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class AotRecordAttribute : Attribute
{
    /// <summary>
    /// Explicitly specify the ID property name if auto-detection fails.
    /// </summary>
    public string? IdPropertyName { get; set; }
    
    /// <summary>
    /// Custom file name for this record type (default: TypeName).
    /// </summary>
    public string? FileName { get; set; }
}
```

**ID Property Discovery Logic:**

1. Use `IdPropertyName` if specified in attribute
2. Find property named "Id"
3. Find property named "{TypeName}Id"
4. Emit analyzer warning if none found

**Analyzer Rules:**

- `CABINET001`: No ID property found (error)
- `CABINET002`: Multiple potential ID properties found (warning)
- `CABINET003`: ID property is not of type string or convertible to string (warning)
- `CABINET004`: Record class is not partial (info)

**NuGet Packaging:**

- Package: `Cabinet.SourceGenerators`
- Include analyzer + generator
- Automatic dependency from main `Cabinet` package
- Works transparently when installed

#### Benefits

- ✅ Zero boilerplate for AOT scenarios
- ✅ Compile-time validation
- ✅ No missing `List<T>` registrations
- ✅ Consistent conventions across projects
- ✅ Better IDE integration (generated code shows in IntelliSense)
- ✅ Reduced onboarding time

#### Timeline

- **Prototype**: 1-2 weeks
- **Testing & refinement**: 1 week
- **Documentation**: 3-4 days
- **Ship**: v2.0.0 (breaking: requires .NET 9+)

---

## Phase 2: Cabinet MCP Server (Medium Priority)

### Goal

Provide AI-powered developer assistance for Cabinet through a Model Context Protocol (MCP) server.

### Vision

Make Cabinet the first offline storage library with native AI agent support. Developers can ask questions, generate code, analyze schemas, and get best practice recommendations directly in their AI coding tools.

### Distribution Strategy

#### Primary: NPM Package

```bash
npm install -g @cabinet/mcp-server
```

**Why NPM:**

- Standard for MCP server distribution
- Works with Claude Desktop, Cline, Copilot, etc.
- Easy updates (`npm update -g @cabinet/mcp-server`)
- Broad compatibility
- Works for any language/platform

**Capabilities:**

- Static analysis of source code
- Template-based code generation
- Documentation and examples
- Schema analysis from database files

**MCP Configuration:**

```json
{
  "mcpServers": {
    "cabinet": {
      "command": "npx",
      "args": ["-y", "@cabinet/mcp-server"]
    }
  }
}
```

#### Secondary: .NET Global Tool

```bash
dotnet tool install -g Cabinet.Mcp
```

**Why .NET Tool:**

- Native integration with .NET ecosystem
- Can leverage Roslyn for deep code analysis
- Direct access to project files and references
- **Can use reflection to explore assemblies at runtime** - answer questions about user's actual types, properties, and code structure without pre-defined schemas
- Better performance for .NET-specific tasks
- Can inspect compiled assemblies to understand existing data models
- Real-time analysis of user's domain objects for migration/optimization suggestions

**Capabilities (Beyond NPM version):**

- **Runtime type inspection via reflection** - see actual compiled types, not just source code
- Discover ID properties, attributes, inheritance hierarchies automatically
- Validate AOT compatibility by inspecting compiled IL
- Analyze user's specific EF Core DbContext or data models
- Generate code based on actual type structures, not templates

**The Key Differentiator:**
The .NET tool can answer "Does Cabinet work with MY types?" by loading and inspecting the user's actual compiled code, while the NPM version requires users to describe their types or relies on static source analysis.

**MCP Configuration:**

```json
{
  "mcpServers": {
    "cabinet": {
      "command": "cabinet-mcp",
      "args": ["--workspace", "${workspaceFolder}"]
    }
  }
}
```

### Features

#### 1. Tools (Actions the AI can perform)

**`analyze_database_schema`**

- Analyzes existing SQLite/Realm/LiteDB schemas
- Returns structure in standardized format
- Input: Connection string or DB file path
- Output: Tables, columns, relationships, indexes

**`analyze_domain_models`** (.NET Tool Only)

- Uses reflection to inspect compiled assemblies and discover user's actual domain types
- Analyzes properties, attributes, complexity, and Cabinet compatibility
- Input: Path to compiled DLL or project directory
- Output: Type inventory with Cabinet recommendations for each type
- **Why it's powerful:** Answers questions about user's specific code without them having to describe it

**`validate_aot_compatibility`** (.NET Tool Enhanced)

- Checks if project is AOT-ready for Cabinet usage
- NPM version: Static analysis of source files
- .NET version: **Uses reflection** to inspect compiled code for runtime issues (missing serializers, reflection usage, etc.)
- Input: Project directory or compiled assembly
- Output: Issues found with line numbers, recommendations with code fixes

**`inspect_type_properties`** (.NET Tool Only)

- Deep inspection of a specific type using reflection
- Discovers ID properties, indexable fields, complex nested objects, file attachment candidates
- Input: Type name or fully qualified type name
- Output: Property-by-property analysis with Cabinet usage recommendations

**`generate_migration_plan`**

- Creates step-by-step migration from existing DB to Cabinet
- .NET version: **Can analyze existing EF Core DbContext or Dapper models via reflection**
- Input: Schema analysis result or assembly path
- Output: Migration strategy, code samples, gotchas specific to user's types

**`create_record_class`**

- Generates C# record class with `[AotRecord]` attribute
- Input: Type name, properties, ID field
- Output: Complete class definition

**`create_recordset_config`**

- Generates RecordSet configuration
- .NET version: **Auto-generates IdSelector by analyzing actual type via reflection**
- Input: Record type name or assembly + type
- Output: RecordSetOptions setup code with proper IdSelector

**`validate_aot_compatibility`**

- Checks if project is AOT-ready
- Input: Project directory
- Output: Issues found, recommendations

**`suggest_indexing_strategy`**

- Recommends index configuration based on query patterns
- Input: Record type, query patterns
- Output: Index configuration code

**`generate_crud_operations`**

- Creates full CRUD implementation for a record type
- Input: Record type name
- Output: Service class with all operations

**`optimize_query`**

- Suggests optimizations for Cabinet queries
- Input: Query code
- Output: Optimized version with explanation

#### 2. Resources (Information the AI can read)

**Documentation:**

- `cabinet://docs/getting-started` - Quick start guide
- `cabinet://docs/recordset` - `RecordSet<T>` usage
- `cabinet://docs/aot` - AOT compatibility guide
- `cabinet://docs/encryption` - Security best practices
- `cabinet://docs/indexing` - Search and indexing
- `cabinet://docs/performance` - Performance tuning

**Examples:**

- `cabinet://examples/lesson-tracking` - Homeschool app pattern
- `cabinet://examples/offline-first` - Offline-first architecture
- `cabinet://examples/sync-pattern` - Sync with remote backend
- `cabinet://examples/migration` - Migrating from SQLite

**Best Practices:**

- `cabinet://best-practices/data-modeling` - Schema design
- `cabinet://best-practices/security` - Key management
- `cabinet://best-practices/testing` - Unit testing strategies
- `cabinet://best-practices/maui-integration` - .NET MAUI patterns

#### 3. Prompts (Pre-packaged workflows)

**"Migrate SQLite to Cabinet"**

- Guides through analysis → migration → testing
- Generates all necessary code
- Provides migration checklist

**"Create new record type"**

- Interactive Q&A for record properties
- Generates class + RecordSet + CRUD operations
- Includes usage examples

**"Optimize Cabinet performance"**

- Analyzes current usage patterns
- Suggests caching strategies
- Recommends index improvements

**"Setup AOT for Cabinet"**

- Checks current configuration
- Generates missing pieces
- Validates final setup

**"Implement offline sync"**

- Designs sync architecture
- Generates conflict resolution code
- Provides testing strategies

### Implementation Architecture

#### NPM Package Structure

```tree
cabinet-mcp-server/
├── package.json
├── src/
│   ├── index.ts              // MCP server entry point
│   ├── server.ts             // MCP protocol implementation
│   ├── tools/
│   │   ├── analyze.ts        // Database analysis
│   │   ├── generate.ts       // Code generation
│   │   ├── validate.ts       // Validation tools
│   │   └── optimize.ts       // Optimization suggestions
│   ├── resources/
│   │   ├── docs.ts           // Documentation provider
│   │   └── examples.ts       // Example code provider
│   ├── prompts/
│   │   └── workflows.ts      // Pre-packaged prompts
│   └── utils/
│       ├── schema-parser.ts  // Parse various DB formats
│       └── code-gen.ts       // Code generation helpers
└── templates/
    ├── record-class.hbs
    ├── recordset-config.hbs
    └── crud-service.hbs
```

#### .NET Tool Structure

```tree
Cabinet.Mcp/
├── Cabinet.Mcp.csproj
├── Program.cs                // CLI entry point
├── McpServer.cs              // MCP protocol
├── Tools/
│   ├── ReflectionAnalyzer.cs // Assembly reflection and type inspection
│   ├── RoslynAnalyzer.cs     // Deep C# source code analysis
│   ├── ProjectAnalyzer.cs    // .csproj analysis
│   └── CodeGenerator.cs      // Roslyn-based codegen
├── Resources/
│   └── DocumentationProvider.cs
└── Templates/
    └── *.scriban              // Scriban templates
```

**Reflection-Powered Analysis Examples:**

```csharp
// Example: analyze_domain_models implementation
public class ReflectionAnalyzer
{
    public DomainModelAnalysis AnalyzeAssembly(string assemblyPath)
    {
        var assembly = Assembly.LoadFrom(assemblyPath);
        var types = assembly.GetExportedTypes()
            .Where(t => t.IsClass && !t.IsAbstract);
        
        var analysis = new List<TypeAnalysis>();
        
        foreach (var type in types)
        {
            var typeAnalysis = new TypeAnalysis
            {
                TypeName = type.Name,
                FullName = type.FullName,
                IdProperty = FindIdProperty(type),
                Properties = AnalyzeProperties(type),
                Complexity = CalculateComplexity(type),
                CabinetCompatibility = CheckCompatibility(type),
                Recommendations = GenerateRecommendations(type)
            };
            
            analysis.Add(typeAnalysis);
        }
        
        return new DomainModelAnalysis { Types = analysis };
    }
    
    private PropertyInfo? FindIdProperty(Type type)
    {
        // Same logic as Cabinet's RecordSet
        return type.GetProperty("Id") 
            ?? type.GetProperty($"{type.Name}Id");
    }
    
    private CompatibilityLevel CheckCompatibility(Type type)
    {
        var properties = type.GetProperties();
        
        // Check for complex types
        if (properties.Any(p => p.PropertyType.IsClass 
            && p.PropertyType != typeof(string) 
            && !p.PropertyType.IsArray))
            return CompatibilityLevel.NeedsFlattening;
        
        // Check for large byte arrays
        if (properties.Any(p => p.PropertyType == typeof(byte[])))
            return CompatibilityLevel.UseAttachments;
        
        return CompatibilityLevel.Perfect;
    }
}

// Example: validate_aot_compatibility implementation
public class AotValidator
{
    public AotValidationResult ValidateAssembly(string assemblyPath)
    {
        var assembly = Assembly.LoadFrom(assemblyPath);
        var issues = new List<AotIssue>();
        
        // Find all RecordSet usages via reflection
        var types = assembly.GetTypes();
        foreach (var type in types)
        {
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
            var recordSetFields = fields.Where(f => 
                f.FieldType.IsGenericType && 
                f.FieldType.GetGenericTypeDefinition().Name.Contains("RecordSet"));
            
            foreach (var field in recordSetFields)
            {
                // Check if IdSelector is used (can't determine from compiled code,
                // but can check for reflection usage patterns)
                var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic);
                var usesReflection = methods.Any(m => 
                    m.GetMethodBody()?.LocalVariables.Any(v => 
                        v.LocalType == typeof(PropertyInfo)) ?? false);
                
                if (usesReflection)
                {
                    issues.Add(new AotIssue
                    {
                        Type = IssueType.MissingIdSelector,
                        Location = $"{type.FullName}.{field.Name}",
                        Recommendation = GenerateIdSelectorCode(field.FieldType)
                    });
                }
            }
        }
        
        // Check for JsonSerializable attributes
        var jsonContexts = types.Where(t => 
            typeof(JsonSerializerContext).IsAssignableFrom(t));
        
        // ... validate all record types have serializers
        
        return new AotValidationResult { Issues = issues };
    }
}
```

### Key Technologies

**NPM Version:**

- TypeScript
- MCP SDK (@modelcontextprotocol/sdk)
- Handlebars for templates
- sql.js for SQLite parsing

**.NET Version:**

- C# 13
- **System.Reflection for runtime assembly inspection**
- Roslyn for code analysis/generation
- Scriban for templates
- System.CommandLine for CLI

### User Experience Examples

#### Example 1: Migration from SQLite

```plaintext
User: "I have a SQLite database with lessons, children, and subjects. 
       Help me migrate to Cabinet."

AI (via MCP):
- Uses analyze_database_schema tool on SQLite file
- Finds 3 tables with relationships
- Uses generate_migration_plan tool
- Returns:
  * 3 record classes with [AotRecord]
  * RecordSet configurations
  * Migration script to copy data
  * Testing checklist
```

#### Example 2: Reflection-Powered Type Analysis (.NET Tool)

```plaintext
User: "Can Cabinet work with my existing domain models?"

AI (via MCP - .NET Tool):
- Uses analyze_domain_models tool with reflection
- Loads user's compiled assembly
- Discovers actual types:
  
  ✅ LessonRecord
     - Has LessonId (string) → Perfect for RecordSet
     - 8 simple properties → Cabinet-friendly
     - Recommendation: Use as-is
  
  ⚠️  StudentRecord
     - Has byte[] ProfileImage (2MB average)
     - Recommendation: Move to FileAttachment
     - Auto-generates migration code
  
  ❌ CourseRecord
     - Has List<Lesson> navigation property
     - Not supported in Cabinet
     - Recommendation: Store LessonIds instead, use RecordCollection
     - Shows code example for flattening

- Returns complete migration plan based on ACTUAL types, not guesses
```

#### Example 3: Smart AOT Validation (.NET Tool)

```plaintext
User: "Is my app ready for AOT compilation with Cabinet?"

AI (via MCP - .NET Tool):
- Uses validate_aot_compatibility with reflection
- Inspects compiled assembly for runtime issues
- Finds:

  ❌ Issue 1: RecordSet<LessonRecord> created without IdSelector
     File: Services/LessonService.cs:23
     Fix: [generates exact code with IdSelector]
  
  ❌ Issue 2: Missing [JsonSerializable(typeof(List<LessonRecord>))]
     File: AppJsonContext.cs
     Fix: [shows exact attribute to add]
  
  ✅ Issue 3: All 12 domain types have proper ID properties
  
  ✅ Issue 4: No reflection usage detected in user code

- Returns checklist with file paths and line numbers
```

#### Example 4: Create New Feature

```plaintext
User: "Add support for tracking student attendance with 
       check-in/check-out times."

AI (via MCP):
- Uses create_record_class for AttendanceRecord
- Uses generate_crud_operations for service class
- Suggests indexing by date and studentId
- Provides usage examples
```

#### Example 3: Performance Optimization

```plaintext
User: "My lesson search is slow. Here's my query code: [paste]"

AI (via MCP):
- Uses optimize_query tool
- Analyzes query patterns
- Suggests:
  * Add index on title and description
  * Use FindAsync instead of GetAllAsync + Where
  * Implement result caching
- Provides optimized code
```

### Implementation Phases

**Phase 2.1: NPM Package MVP** (4-6 weeks)

- Basic MCP server implementation
- Core tools: analyze, generate, validate
- Essential documentation resources
- 2-3 key prompts
- Test with Claude Desktop

**Phase 2.2: Enhanced Features** (3-4 weeks)

- All tools implemented
- Complete documentation set
- All prompt workflows
- Comprehensive examples

**Phase 2.3: .NET Global Tool** (4-5 weeks)

- Port to C#/.NET
- Roslyn-based analysis
- Project file integration
- Enhanced .NET-specific features

**Phase 2.4: VS Code Extension** (Optional, 3-4 weeks)

- Package MCP server with VS Code extension
- UI for common operations
- Integrated documentation viewer

### Success Metrics

- Downloads per month
- GitHub stars on MCP server repo
- Community contributions (templates, examples)
- Mentions in AI coding tool discussions
- Reduced support questions (AI answers them)

## Phase 3: Performance Optimizations (If Needed)

### Philosophy: Stay Focused

Cabinet is **not** trying to be a database. It's a focused tool:

- ✅ Lightweight data storage for mobile apps
- ✅ AOT-friendly and .NET-pure
- ✅ Encrypted at rest
- ✅ Simple full-text search
- ✅ Datasets under ~10,000 records

**When to use Cabinet:** Small-to-medium datasets in mobile/offline-first apps  
**When NOT to use Cabinet:** Large datasets, complex queries, relationships → **Use SQLite/Realm**

### Potential Optimizations (Only if real-world usage demands it)

**Batch Operations:**
If users frequently add/update many records at once, optimize with single disk write:

```csharp
await lessons.AddRangeAsync(newLessons);  // One disk write instead of N
```

**Simple Pagination:**
If memory becomes an issue with large result sets:

```csharp
var page = lessons.Where(filter).Skip(20).Take(10);  // Already works with LINQ
```

**Note:** If you need more than this, you probably need SQLite (or another database), not Cabinet.

## Community Requests

Track feature requests from the community here.

## Contributing

We welcome contributions! If you'd like to work on any of these features:

1. Open an issue to discuss the approach
2. Reference this roadmap in your PR
3. Update this roadmap with any design changes

For the source generator and MCP server, we'll create detailed specification documents once we enter implementation phase.

## Notes

- This roadmap is a living document and will evolve based on community feedback
- Timeline estimates are approximate and subject to change
- Phase priorities may shift based on user needs and contributor availability

**Last Updated:** October 29, 2025
