using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.ELearning.Api.Models;
using Jellyfin.Plugin.ELearning.Courses;
using Jellyfin.Plugin.ELearning.Progress;

namespace Jellyfin.Plugin.ELearning.Api;

/// <summary>
/// Combines a parsed course with Jellyfin's items and the user's progress into the outline the UI
/// shows. Pure, so it is tested without a server.
/// </summary>
public static class OutlineBuilder
{
    /// <summary>
    /// Builds the outline.
    /// </summary>
    /// <param name="seriesId">The course's series ID.</param>
    /// <param name="course">The parsed course.</param>
    /// <param name="episodes">Episodes by lecture relative path.</param>
    /// <param name="seasons">Season IDs by season number.</param>
    /// <param name="completedDocuments"><see cref="ProgressStore.Key"/>s of the document lectures the user completed.</param>
    /// <param name="facts">Facts about a file, by its relative path and kind.</param>
    /// <returns>The outline.</returns>
    public static CourseOutline Build(
        Guid seriesId,
        Course course,
        IReadOnlyDictionary<string, EpisodeState> episodes,
        IReadOnlyDictionary<int, Guid> seasons,
        IReadOnlySet<string> completedDocuments,
        Func<string, FileKind, FileFacts> facts)
    {
        ArgumentNullException.ThrowIfNull(course);
        ArgumentNullException.ThrowIfNull(episodes);
        ArgumentNullException.ThrowIfNull(seasons);
        ArgumentNullException.ThrowIfNull(completedDocuments);
        ArgumentNullException.ThrowIfNull(facts);

        List<OutlineFile> Files(IEnumerable<CourseFile> files)
            => files.Select(f =>
            {
                var fact = facts(f.RelativePath, f.Kind);
                return new OutlineFile(f.Id, f.DisplayName, f.Kind, fact.Size, fact.ExternalUrl);
            }).ToList();

        OutlineLecture Lecture(CourseLecture lecture)
        {
            var attachments = Files(lecture.Attachments);
            if (lecture.Kind == FileKind.Video)
            {
                var episode = episodes.GetValueOrDefault(lecture.RelativePath);
                return new OutlineLecture(
                    lecture.Id,
                    lecture.Kind,
                    lecture.Number,
                    lecture.Title,
                    episode?.ItemId,
                    episode?.Played ?? false,
                    episode?.PositionTicks,
                    episode?.RunTimeTicks,
                    null,
                    attachments);
            }

            return new OutlineLecture(
                lecture.Id,
                lecture.Kind,
                lecture.Number,
                lecture.Title,
                null,
                completedDocuments.Contains(ProgressStore.Key(course.FolderName, lecture.RelativePath)),
                null,
                null,
                facts(lecture.RelativePath, lecture.Kind).ExternalUrl,
                attachments);
        }

        var sections = course.Sections.Select(s =>
        {
            var lectures = s.Lectures.Select(Lecture).ToList();
            return new OutlineSection(
                s.Title,
                s.Number,
                s.SeasonNumber is { } season && seasons.TryGetValue(season, out var seasonId) ? seasonId : null,
                Totals(lectures),
                lectures,
                Files(s.Resources));
        }).ToList();

        return new CourseOutline(
            seriesId,
            course.Title,
            course.Studio,
            course.Year,
            Totals(sections.SelectMany(s => s.Lectures)),
            Files(course.Resources),
            sections);
    }

    private static ProgressTotals Totals(IEnumerable<OutlineLecture> lectures)
    {
        int total = 0, completed = 0;
        foreach (var lecture in lectures)
        {
            total++;
            completed += lecture.Completed ? 1 : 0;
        }

        return new ProgressTotals(total, completed);
    }
}
