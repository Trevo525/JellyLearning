using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.ELearning.Api.Models;

/// <summary>
/// A section in the outline.
/// </summary>
/// <param name="Title">Title.</param>
/// <param name="Number">Number from the folder name, if any.</param>
/// <param name="SeasonId">Jellyfin season ID; null for sections without videos (quiz-only).</param>
/// <param name="Progress">Section progress.</param>
/// <param name="Lectures">Lectures in course order.</param>
/// <param name="Resources">Section-level files.</param>
public sealed record OutlineSection(
    string Title,
    int? Number,
    Guid? SeasonId,
    ProgressTotals Progress,
    IReadOnlyList<OutlineLecture> Lectures,
    IReadOnlyList<OutlineFile> Resources);
