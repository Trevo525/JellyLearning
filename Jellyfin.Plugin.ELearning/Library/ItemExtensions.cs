using System;
using System.Linq;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.ELearning.Library;

/// <summary>
/// Helpers for Jellyfin items.
/// </summary>
internal static class ItemExtensions
{
    /// <summary>
    /// Gets a value indicating whether the user locked <paramref name="field"/> (edited it by hand).
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="field">The field.</param>
    /// <returns>True when the plugin must not overwrite the field.</returns>
    public static bool IsFieldLocked(this BaseItem item, MetadataField field)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.IsLocked || item.LockedFields.Contains(field);
    }
}
