using System;
using Jellyfin.Plugin.ELearning.Courses;

namespace Jellyfin.Plugin.ELearning.Api.Models;

/// <summary>
/// A lecture attachment or a section/course resource.
/// </summary>
/// <param name="Id">File ID for <c>Files/{id}</c>.</param>
/// <param name="Name">Display name.</param>
/// <param name="Kind">The file kind.</param>
/// <param name="Size">Size in bytes, or null when the file is missing.</param>
/// <param name="ExternalUrl">For link stubs: the external page to open instead of viewing the file.</param>
public sealed record OutlineFile(string Id, string Name, FileKind Kind, long? Size, Uri? ExternalUrl);
