using System;
using System.Collections.Immutable;
using Palmier.Domain;

namespace Palmier.Application.State;

/// Immutable selection state. Tracks which clips, tracks, and range are selected,
/// plus the active track for new insertions.
public sealed record SelectionState
{
    public ImmutableHashSet<string> SelectedClipIds { get; init; } = ImmutableHashSet<string>.Empty;
    public ImmutableHashSet<string> SelectedTrackIds { get; init; } = ImmutableHashSet<string>.Empty;
    public string? ActiveTrackId { get; init; }
    public TimelineRangeSelection? RangeSelection { get; init; }

    public bool IsClipSelected(string clipId)  => SelectedClipIds.Contains(clipId);
    public bool IsTrackSelected(string trackId) => SelectedTrackIds.Contains(trackId);
    public bool HasSelection => !SelectedClipIds.IsEmpty || RangeSelection is { IsValid: true };

    // ── Transition methods ──────────────────────────────────────────────────

    public SelectionState SelectClip(string clipId, bool additive) =>
        this with
        {
            SelectedClipIds = additive
                ? SelectedClipIds.Add(clipId)
                : ImmutableHashSet.Create(clipId),
            RangeSelection = null
        };

    public SelectionState DeselectClip(string clipId) =>
        this with { SelectedClipIds = SelectedClipIds.Remove(clipId) };

    public SelectionState SelectClips(IEnumerable<string> clipIds, bool additive) =>
        this with
        {
            SelectedClipIds = additive
                ? SelectedClipIds.Union(clipIds)
                : ImmutableHashSet.CreateRange(clipIds),
            RangeSelection = null
        };

    public SelectionState ClearClipSelection() =>
        this with { SelectedClipIds = ImmutableHashSet<string>.Empty };

    public SelectionState SetActiveTrack(string? trackId) =>
        this with { ActiveTrackId = trackId };

    public SelectionState SetRangeSelection(TimelineRangeSelection? range) =>
        this with { RangeSelection = range, SelectedClipIds = ImmutableHashSet<string>.Empty };

    public SelectionState ClearAll() =>
        this with
        {
            SelectedClipIds  = ImmutableHashSet<string>.Empty,
            SelectedTrackIds = ImmutableHashSet<string>.Empty,
            RangeSelection   = null
        };
}
