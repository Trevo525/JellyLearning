using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.ELearning.Library;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ELearning.Progress;

/// <summary>
/// Applies <see cref="CompletionRule"/> to lectures in course libraries. Jellyfin saves its own
/// verdict first and then raises the playback events, so the guard re-saves the corrected state
/// on every progress report and on stop.
/// </summary>
public sealed class ProgressGuard : IHostedService, IDisposable
{
    private readonly ISessionManager _sessionManager;
    private readonly IUserDataManager _userDataManager;
    private readonly CourseCatalog _catalog;
    private readonly ILogger<ProgressGuard> _logger;

    // Played state before the current playback, so rewatching a completed lecture keeps it complete
    // even though Jellyfin may have flipped Played during the playback.
    private readonly ConcurrentDictionary<(Guid User, Guid Item), bool> _playedAtStart = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ProgressGuard"/> class.
    /// </summary>
    /// <param name="sessionManager">The session manager.</param>
    /// <param name="userDataManager">The user data manager.</param>
    /// <param name="catalog">The course catalog.</param>
    /// <param name="logger">The logger.</param>
    public ProgressGuard(ISessionManager sessionManager, IUserDataManager userDataManager, CourseCatalog catalog, ILogger<ProgressGuard> logger)
    {
        _sessionManager = sessionManager;
        _userDataManager = userDataManager;
        _catalog = catalog;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _userDataManager.UserDataSaved += OnUserDataSaved;
        _sessionManager.PlaybackProgress += OnPlaybackProgress;
        _sessionManager.PlaybackStopped += OnPlaybackStopped;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        Dispose();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _userDataManager.UserDataSaved -= OnUserDataSaved;
        _sessionManager.PlaybackProgress -= OnPlaybackProgress;
        _sessionManager.PlaybackStopped -= OnPlaybackStopped;
    }

    // ISessionManager.PlaybackStart is raised asynchronously and can arrive after the first progress
    // report, by which time Jellyfin may already have flipped Played. The user-data save Jellyfin does
    // at playback start is synchronous and leaves Played untouched for videos, so snapshot it there.
    private void OnUserDataSaved(object? sender, UserDataSaveEventArgs e)
    {
        if (e.SaveReason == UserDataSaveReason.PlaybackStart && IsLecture(e.Item))
        {
            _playedAtStart[(e.UserId, e.Item.Id)] = e.UserData.Played;
        }
    }

    private void OnPlaybackProgress(object? sender, PlaybackProgressEventArgs e)
    {
        if (!e.IsAutomated)
        {
            Apply(e, UserDataSaveReason.PlaybackProgress, stopped: false);
        }
    }

    private void OnPlaybackStopped(object? sender, PlaybackStopEventArgs e) => Apply(e, UserDataSaveReason.PlaybackFinished, stopped: true);

    private void Apply(PlaybackProgressEventArgs e, UserDataSaveReason reason, bool stopped)
    {
        if (!IsLecture(e.Item))
        {
            return;
        }

        var config = Plugin.Instance?.Configuration;
        if (config is null)
        {
            return;
        }

        try
        {
            foreach (var user in e.Users)
            {
                var key = (user.Id, e.Item.Id);
                var data = _userDataManager.GetUserData(user, e.Item);
                if (data is null)
                {
                    continue;
                }

                var wasPlayed = _playedAtStart.TryGetValue(key, out var atStart) ? atStart : data.Played;
                if (stopped)
                {
                    _playedAtStart.TryRemove(key, out _);
                }

                var verdict = CompletionRule.Evaluate(
                    e.PlaybackPositionTicks,
                    e.Item.GetRunTimeTicksForPlayState(),
                    wasPlayed,
                    config.CompletionSecondsFromEnd,
                    config.CompletionPercent);
                if (verdict is not { } state || (data.Played == state.Played && data.PlaybackPositionTicks == state.PositionTicks))
                {
                    continue;
                }

                Save(user, e.Item, data, state.Played, state.PositionTicks, reason);
            }
        }
        catch (Exception ex)
        {
            // Never break playback reporting because of the guard.
            _logger.LogError(ex, "Could not apply the course completion rule to {Item}", e.Item.Path);
        }
    }

    private void Save(User user, BaseItem item, UserItemData data, bool played, long positionTicks, UserDataSaveReason reason)
    {
        _logger.LogDebug(
            "Course rule for {User} / {Item}: played {OldPlayed}->{Played}, position {OldPosition}->{Position}",
            user.Username,
            item.Name,
            data.Played,
            played,
            data.PlaybackPositionTicks,
            positionTicks);
        data.Played = played;
        data.PlaybackPositionTicks = positionTicks;
        _userDataManager.SaveUserData(user, item, data, reason, CancellationToken.None);
    }

    private bool IsLecture(BaseItem? item)
        => item is Video && _catalog.GetCourseFor(item.Path)?.GetLecture(item.Path) is not null;
}
