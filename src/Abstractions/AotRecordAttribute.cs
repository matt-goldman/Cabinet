using System;

namespace Cabinet;

/// <summary>
/// Marks a class as an AOT-compatible record type for Cabinet.
/// Automatically generates JSON serialisation context and RecordSet configuration.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class AotRecordAttribute : Attribute
{
	/// <summary>
	/// Explicitly specify the ID property name if auto-detection fails.
	/// Auto-detection looks for properties named "Id" or "{TypeName}Id".
	/// </summary>
	public string? IdPropertyName { get; set; }

	/// <summary>
	/// Custom file name for this record type (default: TypeName).
	/// </summary>
	public string? FileName { get; set; }
}
