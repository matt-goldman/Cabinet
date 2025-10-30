using System.Text.Json.Serialization;
using demo.Models;

namespace demo;

/// <summary>
/// AOT-safe JSON serialisation context for Cabinet offline data.
/// This must be manually created to ensure System.Text.Json's source generator can process it.
/// </summary>
[JsonSerializable(typeof(LessonRecord))]
[JsonSerializable(typeof(List<LessonRecord>))]
[JsonSourceGenerationOptions(
	WriteIndented = false,
	PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
	DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
public partial class CabinetJsonContext : JsonSerializerContext
{
}
