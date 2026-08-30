using demo.Models;

namespace demo.Pages;

public partial class ResultPage : ContentPage
{
    public ResultPage(LessonRecord? lesson, StudentRecord? student)
    {
        InitializeComponent();

        if (lesson != null)
        {
            Title.Text = "Lesson Record";
            Name.Text = lesson.Subject;

            if (lesson.Attachments?.Count > 0)
            {
                Photo.Source = ImageSource.FromStream(() => lesson.Attachments[0].Content);
            }
        }

        if (student == null) return;
        
        Title.Text = "Student Record";
        Name.Text = student.Name;

        if (student.ProfilePhoto?.Content != null)
        {
            Photo.Source = ImageSource.FromStream(() => student.ProfilePhoto?.Content);
        }
    }
}