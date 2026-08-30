using demo.Models;
using demo.Services;

namespace demo.Pages;

public partial class ResultPage : ContentPage
{
	public ResultPage(OfflineDataService dataService, LessonRecord? lesson, StudentRecord? student)
	{
		InitializeComponent();

		if (lesson != null)
		{
			Title.Text = "Lesson Record";
			Name.Text = lesson.Subject;

			if (lesson.Attachments?.Count > 0)
			{
				_ = LoadPhotoAsync(() => dataService.ReadLessonAttachmentAsync(lesson.Id, lesson.Attachments[0]));
			}
		}

		if (student == null) return;

		Title.Text = "Student Record";
		Name.Text = student.Name;

		if (student.ProfilePhoto != null)
		{
			_ = LoadPhotoAsync(() => dataService.ReadStudentAttachmentAsync(student.Id, student.ProfilePhoto));
		}
	}

	/// <summary>
	/// Reads the attachment's bytes back out of the encrypted store and displays them. The record
	/// itself carries only the metadata, so the content is fetched on demand.
	/// </summary>
	private async Task LoadPhotoAsync(Func<Task<byte[]?>> read)
	{
		var content = await read();
		if (content is null) return;

		Photo.Source = ImageSource.FromStream(() => new MemoryStream(content));
	}
}
