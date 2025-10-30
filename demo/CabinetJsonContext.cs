using System.Text.Json.Serialization;
using demo.Models;

namespace demo;

/// <summary>
/// AOT-safe JSON serialisation context for Cabinet offline data.
/// This must be manually created to ensure System.Text.Json's source generator can process it.
/// Add all record types here for AOT compatibility.
/// </summary>
[JsonSerializable(typeof(LessonRecord))]
[JsonSerializable(typeof(List<LessonRecord>))]
[JsonSerializable(typeof(StudentRecord))]
[JsonSerializable(typeof(List<StudentRecord>))]
[JsonSourceGenerationOptions(
	WriteIndented = false,
	PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
	DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
public partial class CabinetJsonContext : JsonSerializerContext
{
}
