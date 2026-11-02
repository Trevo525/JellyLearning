using System.Globalization;
using System.Text;
using Jellyfin.Plugin.ELearning.Courses;

namespace Jellyfin.Plugin.ELearning.Tests;

/// <summary>Renders a parsed course as text, for snapshot tests and review.</summary>
internal static class OutlineRenderer
{
    public static string Render(Course course)
    {
        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"COURSE \"{course.Title}\"  studio={course.Studio ?? "-"}  year={course.Year?.ToString(CultureInfo.InvariantCulture) ?? "-"}  artwork={course.ArtworkPath ?? "-"}");
        foreach (var file in course.Resources)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"  resource  {File(file)}");
        }

        foreach (var section in course.Sections)
        {
            var season = section.SeasonNumber is { } s ? $"S{s}" : "S-";
            sb.AppendLine();
            sb.AppendLine(CultureInfo.InvariantCulture, $"{season}  #{section.Number?.ToString(CultureInfo.InvariantCulture) ?? "-"}  \"{section.Title}\"  <- {(section.RelativePath.Length == 0 ? "(course folder)" : section.RelativePath)}");
            foreach (var lecture in section.Lectures)
            {
                var episode = lecture.EpisodeNumber is { } e ? $"E{e}" : "--";
                sb.AppendLine(CultureInfo.InvariantCulture, $"  {episode,-4} #{lecture.Number?.ToString(CultureInfo.InvariantCulture) ?? "-",-4} {lecture.Kind,-8} \"{lecture.Title}\"  <- {lecture.RelativePath}");
                foreach (var sub in lecture.Subtitles)
                {
                    sb.AppendLine(CultureInfo.InvariantCulture, $"         subtitle  {sub.Format} lang={sub.Language ?? "-"}  <- {sub.RelativePath}");
                }

                foreach (var file in lecture.Attachments)
                {
                    sb.AppendLine(CultureInfo.InvariantCulture, $"         attach    {File(file)}");
                }
            }

            foreach (var file in section.Resources)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"  resource  {File(file)}");
            }
        }

        return sb.ToString();
    }

    private static string File(CourseFile file) => $"{file.Kind,-8} \"{file.DisplayName}\"  <- {file.RelativePath}";
}
