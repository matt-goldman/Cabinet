using System.Security.Cryptography;
using System.Text;

namespace Cabinet.Core;

/// <summary>
/// Validates attachment logical names and maps them to on-disk file names.
/// </summary>
/// <remarks>
/// Logical names are never used directly as path segments. They are hashed, which keeps arbitrary
/// user-supplied names from escaping the attachments directory or colliding with the naming of
/// other records' attachments, and keeps path lengths predictable across platforms.
/// </remarks>
internal static class AttachmentName
{
	/// <summary>
	/// The maximum permitted length, in characters, of an attachment logical name.
	/// </summary>
	internal const int MaxLength = 255;

	/// <summary>
	/// Throws if the supplied logical name cannot be used as an attachment name.
	/// </summary>
	/// <param name="name">The logical name to validate</param>
	/// <param name="paramName">The name of the parameter being validated, for the exception message</param>
	/// <exception cref="ArgumentException">Thrown if the name is empty, too long, or contains path syntax</exception>
	internal static void Validate(string name, string paramName)
	{
		if (string.IsNullOrWhiteSpace(name))
			throw new ArgumentException("Attachment name must not be null or whitespace.", paramName);

		if (name.Length > MaxLength)
			throw new ArgumentException($"Attachment name must be {MaxLength} characters or fewer.", paramName);

		if (name is "." or "..")
			throw new ArgumentException("Attachment name must not be '.' or '..'.", paramName);

		if (name.Contains('/') || name.Contains('\\'))
			throw new ArgumentException("Attachment name must not contain path separators.", paramName);

		foreach (var c in name)
		{
			if (char.IsControl(c))
				throw new ArgumentException("Attachment name must not contain control characters.", paramName);
		}
	}

	/// <summary>
	/// Maps an arbitrary string to a file-system-safe, fixed-length directory or file name.
	/// </summary>
	/// <param name="value">The value to map</param>
	/// <returns>A 32-character lowercase hexadecimal string</returns>
	internal static string ToFileName(string value)
	{
		var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
		// 128 bits is ample for local addressing and keeps total path length well inside
		// the limits imposed by Windows and the mobile platforms.
		return Convert.ToHexStringLower(hash.AsSpan(0, 16));
	}
}
