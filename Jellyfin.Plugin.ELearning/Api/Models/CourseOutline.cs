using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.ELearning.Api.Models;

/// <summary>
/// The full outline of a course with the requesting user's progress.
/// </summary>
/// <param name="SeriesId">Jellyfin series ID of the course.</param>
/// <param name="Title">Course title.</param>
/// <param name="Studio">Provider, if recognised.</param>
/// <param name="Year">Year, if known.</param>
/// <param name="Progress">Course progress over all lectures (videos and documents).</param>
/// <param name="Resources">Course-level files.</param>
/// <param name="Sections">Sections in course order.</param>
public sealed record CourseOutline(
    Guid SeriesId,
    string Title,
    string? Studio,
    int? Year,
    ProgressTotals Progress,
    IReadOnlyList<OutlineFile> Resources,
    IReadOnlyList<OutlineSection> Sections);
