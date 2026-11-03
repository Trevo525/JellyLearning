using System;

namespace Jellyfin.Plugin.ELearning.Api;

/// <summary>
/// What the outline shows about a file on disk.
/// </summary>
/// <param name="Size">Size in bytes, or null when the file is missing.</param>
/// <param name="ExternalUrl">Target of a link stub, if the file is one.</param>
public sealed record FileFacts(long? Size, Uri? ExternalUrl);
