using System;
using System.Collections.Generic;
using Jellyfin.Plugin.ELearning.Courses;

namespace Jellyfin.Plugin.ELearning.Api.Models;

/// <summary>
/// A lecture in the outline: a video (a Jellyfin episode) or a document lecture.
/// </summary>
/// <param name="Id">Lecture ID: the file ID of its main file, also used for <c>Lectures/{id}/Complete</c>.</param>
/// <param name="Kind">Kind of the main file.</param>
/// <param name="Number">Number from the file name, if any.</param>
/// <param name="Title">Title.</param>
/// <param name="ItemId">Jellyfin episode ID (videos only; null until the library scan has added it).</param>
/// <param name="Completed">Whether the user has completed the lecture.</param>
/// <param name="PositionTicks">Resume position (videos only).</param>
/// <param name="RunTimeTicks">Duration (videos only, once probed).</param>
/// <param name="ExternalUrl">For link-stub lectures: the external page to open.</param>
/// <param name="Attachments">Files of this lecture.</param>
public sealed record OutlineLecture(
    string Id,
    FileKind Kind,
    int? Number,
    string Title,
    Guid? ItemId,
    bool Completed,
    long? PositionTicks,
    long? RunTimeTicks,
    Uri? ExternalUrl,
    IReadOnlyList<OutlineFile> Attachments);
