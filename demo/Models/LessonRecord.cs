using Plugin.Maui.OfflineData.Core;

namespace demo.Models;

public class LessonRecord
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);
	public string Subject { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public List<string> Children { get; set; } = [];
	public List<string> Tags { get; set; } = [];
	public List<FileAttachment>? Attachments { get; set; }
}
