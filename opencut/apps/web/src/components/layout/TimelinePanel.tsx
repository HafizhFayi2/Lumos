import React, { useRef, useCallback, useEffect, useState } from 'react';
import {
  Plus, Undo2, Redo2, Scissors, Trash2,
  Bookmark, Link2, ZoomIn, ZoomOut,
  Video, Music, Eye, EyeOff, Lock, Unlock, MoreHorizontal, Image as ImageIcon
} from 'lucide-react';
import { Button } from '../ui/button';
import { useEditor, formatTime } from '../../store/editor-store';
import type { TrackItem } from '../../store/editor-store';

// ─── Zoom Slider ──────────────────────────────────────────────────────────────

function ZoomSlider() {
  const { state, dispatch } = useEditor();
  const trackRef = useRef<HTMLDivElement>(null);
  const MIN = 20;
  const MAX = 300;
  const pct = ((state.zoom - MIN) / (MAX - MIN)) * 100;

  function getZoom(e: MouseEvent | React.MouseEvent) {
    const rect = trackRef.current?.getBoundingClientRect();
    if (!rect) return state.zoom;
    const p = Math.max(0, Math.min(1, (e.clientX - rect.left) / rect.width));
    return Math.round(MIN + p * (MAX - MIN));
  }

  function onMouseDown(e: React.MouseEvent) {
    dispatch({ type: 'SET_ZOOM', zoom: getZoom(e) });
    function onMove(ev: MouseEvent) { dispatch({ type: 'SET_ZOOM', zoom: getZoom(ev) }); }
    function onUp() { window.removeEventListener('mousemove', onMove); window.removeEventListener('mouseup', onUp); }
    window.addEventListener('mousemove', onMove);
    window.addEventListener('mouseup', onUp);
  }

  return (
    <div
      ref={trackRef}
      className="w-24 h-1.5 bg-white/10 rounded-full relative mx-1 cursor-pointer"
      onMouseDown={onMouseDown}
    >
      <div className="absolute left-0 top-0 bottom-0 bg-indigo-500 rounded-full" style={{ width: `${pct}%` }} />
      <div
        className="absolute top-1/2 -translate-y-1/2 w-3 h-3 bg-indigo-400 rounded-full shadow border border-white/20"
        style={{ left: `calc(${pct}% - 6px)` }}
      />
    </div>
  );
}

// ─── Timeline Ruler ───────────────────────────────────────────────────────────

function TimelineRuler({ zoom, duration, onSeek }: { zoom: number; duration: number; onSeek: (t: number) => void }) {
  const rulerRef = useRef<HTMLDivElement>(null);

  // Tick interval: aim for ~80px between ticks
  const rawInterval = 80 / zoom; // seconds per tick
  const intervals = [0.1, 0.25, 0.5, 1, 2, 5, 10, 15, 30, 60, 120];
  const interval = intervals.find(i => i >= rawInterval) ?? 120;

  const totalWidth = duration * zoom;
  const ticks: number[] = [];
  for (let t = 0; t <= duration; t += interval) ticks.push(Math.round(t * 1000) / 1000);

  function handleClick(e: React.MouseEvent) {
    const rect = rulerRef.current?.getBoundingClientRect();
    if (!rect) return;
    const t = (e.clientX - rect.left) / zoom;
    onSeek(t);
  }

  return (
    <div
      ref={rulerRef}
      className="h-6 border-b border-white/5 sticky top-0 bg-[#121217] z-10 text-[10px] text-zinc-500 font-mono relative cursor-pointer select-none"
      style={{ width: Math.max(totalWidth, 800) }}
      onClick={handleClick}
    >
      {ticks.map(t => (
        <div
          key={t}
          className="absolute top-0 bottom-0 flex items-end pb-1 border-l border-white/10"
          style={{ left: t * zoom }}
        >
          <span className="pl-1">{formatTime(t)}</span>
        </div>
      ))}
    </div>
  );
}

// ─── Track Clip ───────────────────────────────────────────────────────────────

function TimelineClip({
  item,
  zoom,
  isSelected,
  assetName,
  assetType,
  onSelect,
  onMove,
}: {
  item: TrackItem;
  zoom: number;
  isSelected: boolean;
  assetName: string;
  assetType: string;
  onSelect: () => void;
  onMove: (newStart: number) => void;
}) {
  const dragStart = useRef<{ mouseX: number; itemStart: number } | null>(null);

  const colorClass =
    assetType === 'audio' ? 'bg-emerald-500/20 border-emerald-500/40' :
    assetType === 'image' ? 'bg-purple-500/20 border-purple-500/40' :
    'bg-blue-500/20 border-blue-500/40';

  const selectedClass = isSelected ? 'ring-1 ring-indigo-400 ring-offset-1 ring-offset-[#121217]' : '';

  function onMouseDown(e: React.MouseEvent) {
    e.stopPropagation();
    onSelect();
    dragStart.current = { mouseX: e.clientX, itemStart: item.startTime };

    function onMove(ev: MouseEvent) {
      if (!dragStart.current) return;
      const delta = (ev.clientX - dragStart.current.mouseX) / zoom;
      const newStart = Math.max(0, dragStart.current.itemStart + delta);
      onMove(newStart);
    }
    function onUp() {
      dragStart.current = null;
      window.removeEventListener('mousemove', onMove);
      window.removeEventListener('mouseup', onUp);
    }
    window.addEventListener('mousemove', onMove);
    window.addEventListener('mouseup', onUp);
  }

  return (
    <div
      className={`absolute top-1 bottom-1 rounded-md border ${colorClass} ${selectedClass} cursor-grab active:cursor-grabbing overflow-hidden select-none`}
      style={{
        left: item.startTime * zoom,
        width: Math.max(item.duration * zoom - 2, 20),
      }}
      onMouseDown={onMouseDown}
      title={assetName}
    >
      <div className="h-full flex items-center px-2">
        <span className="text-[10px] text-white/70 truncate">{assetName}</span>
      </div>
      {/* Resize handle right */}
      <div className="absolute right-0 top-0 bottom-0 w-2 cursor-ew-resize bg-white/10 hover:bg-white/30 transition-colors" />
    </div>
  );
}

// ─── Playhead ─────────────────────────────────────────────────────────────────

function Playhead({ currentTime, zoom, onSeek }: { currentTime: number; zoom: number; onSeek: (t: number) => void }) {
  const dragStart = useRef<{ mouseX: number; startTime: number } | null>(null);
  const left = currentTime * zoom;

  function onMouseDown(e: React.MouseEvent) {
    e.preventDefault();
    dragStart.current = { mouseX: e.clientX, startTime: currentTime };
    function onMove(ev: MouseEvent) {
      if (!dragStart.current) return;
      const delta = (ev.clientX - dragStart.current.mouseX) / zoom;
      onSeek(Math.max(0, dragStart.current.startTime + delta));
    }
    function onUp() {
      dragStart.current = null;
      window.removeEventListener('mousemove', onMove);
      window.removeEventListener('mouseup', onUp);
    }
    window.addEventListener('mousemove', onMove);
    window.addEventListener('mouseup', onUp);
  }

  return (
    <div className="absolute top-0 bottom-0 z-20 pointer-events-none" style={{ left }}>
      {/* Head triangle */}
      <div
        className="absolute -top-0 left-1/2 -translate-x-1/2 w-4 h-3 pointer-events-auto cursor-ew-resize"
        onMouseDown={onMouseDown}
      >
        <svg viewBox="0 0 16 12" className="w-full h-full fill-indigo-400">
          <polygon points="0,0 16,0 8,12" />
        </svg>
      </div>
      {/* Vertical line */}
      <div className="absolute top-3 bottom-0 left-1/2 -translate-x-1/2 w-px bg-indigo-500" />
    </div>
  );
}

// ─── TimelinePanel ────────────────────────────────────────────────────────────

export function TimelinePanel() {
  const { state, dispatch, setCurrentTime, togglePlay } = useEditor();
  const scrollRef = useRef<HTMLDivElement>(null);
  const [bookmarkActive, setBookmarkActive] = useState(false);
  const [linkActive, setLinkActive] = useState(false);

  // Space bar → play/pause
  useEffect(() => {
    function onKey(e: KeyboardEvent) {
      if (e.target instanceof HTMLInputElement || e.target instanceof HTMLTextAreaElement) return;
      if (e.key === ' ') { e.preventDefault(); togglePlay(); }
      if ((e.ctrlKey || e.metaKey) && e.key === 'z') { e.preventDefault(); dispatch({ type: 'UNDO' }); }
      if ((e.ctrlKey || e.metaKey) && (e.key === 'y' || (e.shiftKey && e.key === 'z'))) { e.preventDefault(); dispatch({ type: 'REDO' }); }
      if (e.key === 'Delete' || e.key === 'Backspace') {
        if (state.selectedItemId) dispatch({ type: 'DELETE_SELECTED_ITEM' });
      }
    }
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [dispatch, togglePlay, state.selectedItemId]);

  // Auto-scroll playhead into view
  useEffect(() => {
    if (!scrollRef.current || !state.isPlaying) return;
    const playheadLeft = state.currentTime * state.zoom;
    const { scrollLeft, clientWidth } = scrollRef.current;
    if (playheadLeft < scrollLeft || playheadLeft > scrollLeft + clientWidth - 40) {
      scrollRef.current.scrollLeft = playheadLeft - clientWidth / 2;
    }
  }, [state.currentTime, state.zoom, state.isPlaying]);

  const handleSeek = useCallback((t: number) => {
    setCurrentTime(t);
    if (state.isPlaying) dispatch({ type: 'SET_PLAYING', playing: false });
  }, [setCurrentTime, state.isPlaying, dispatch]);

  const handleItemMove = useCallback((id: string, newStart: number) => {
    dispatch({ type: 'UPDATE_ITEM', id, updates: { startTime: newStart } });
  }, [dispatch]);

  const totalWidth = Math.max(state.duration * state.zoom, 800);
  const hasItems = state.items.length > 0;

  return (
    <div className="h-[300px] border-t border-white/5 bg-[#121217] flex flex-col shrink-0">
      {/* Timeline Toolbar */}
      <div className="h-10 border-b border-white/5 flex items-center justify-between px-4">
        <div className="flex items-center gap-1">
          {/* Add track */}
          <Button
            variant="ghost"
            size="icon"
            className="w-7 h-7 text-zinc-400 hover:text-white"
            title="Add track"
            onClick={() => dispatch({ type: 'ADD_TRACK' })}
          >
            <Plus className="w-4 h-4" />
          </Button>
          <div className="w-px h-4 bg-white/10 mx-1" />

          {/* Undo */}
          <Button
            variant="ghost"
            size="icon"
            className={`w-7 h-7 hover:text-white ${state.undoStack.length > 0 ? 'text-zinc-400' : 'text-zinc-600 opacity-40'}`}
            title="Undo (Ctrl+Z)"
            onClick={() => dispatch({ type: 'UNDO' })}
            disabled={state.undoStack.length === 0}
          >
            <Undo2 className="w-4 h-4" />
          </Button>

          {/* Redo */}
          <Button
            variant="ghost"
            size="icon"
            className={`w-7 h-7 hover:text-white ${state.redoStack.length > 0 ? 'text-zinc-400' : 'text-zinc-600 opacity-40'}`}
            title="Redo (Ctrl+Y)"
            onClick={() => dispatch({ type: 'REDO' })}
            disabled={state.redoStack.length === 0}
          >
            <Redo2 className="w-4 h-4" />
          </Button>
          <div className="w-px h-4 bg-white/10 mx-1" />

          {/* Scissors (split) */}
          <Button
            variant="ghost"
            size="icon"
            className={`w-7 h-7 hover:text-white ${state.selectedItemId ? 'text-zinc-400' : 'text-zinc-600 opacity-40'}`}
            title="Split clip at playhead"
            onClick={() => dispatch({ type: 'SPLIT_ITEM_AT_PLAYHEAD' })}
            disabled={!state.selectedItemId}
          >
            <Scissors className="w-4 h-4" />
          </Button>

          {/* Delete */}
          <Button
            variant="ghost"
            size="icon"
            className={`w-7 h-7 hover:text-red-400 ${state.selectedItemId ? 'text-zinc-400' : 'text-zinc-600 opacity-40'}`}
            title="Delete selected (Del)"
            onClick={() => dispatch({ type: 'DELETE_SELECTED_ITEM' })}
            disabled={!state.selectedItemId}
          >
            <Trash2 className="w-4 h-4" />
          </Button>
          <div className="w-px h-4 bg-white/10 mx-1" />

          {/* Bookmark */}
          <Button
            variant="ghost"
            size="icon"
            className={`w-7 h-7 hover:text-white ${bookmarkActive ? 'text-indigo-400' : 'text-zinc-400'}`}
            title="Bookmark"
            onClick={() => setBookmarkActive(v => !v)}
          >
            <Bookmark className="w-4 h-4" />
          </Button>

          {/* Link */}
          <Button
            variant="ghost"
            size="icon"
            className={`w-7 h-7 hover:text-white ${linkActive ? 'text-indigo-400' : 'text-zinc-400'}`}
            title="Link/Unlink clips"
            onClick={() => setLinkActive(v => !v)}
          >
            <Link2 className="w-4 h-4" />
          </Button>
        </div>

        <div className="flex items-center gap-2">
          {/* Current time display */}
          <span className="text-[10px] text-zinc-500 font-mono">{formatTime(state.currentTime)}</span>

          {/* Zoom controls */}
          <Button
            variant="ghost"
            size="icon"
            className="w-7 h-7 text-zinc-400 hover:text-white"
            title="Zoom out"
            onClick={() => dispatch({ type: 'SET_ZOOM', zoom: state.zoom * 0.75 })}
          >
            <ZoomOut className="w-4 h-4" />
          </Button>
          <ZoomSlider />
          <Button
            variant="ghost"
            size="icon"
            className="w-7 h-7 text-zinc-400 hover:text-white"
            title="Zoom in"
            onClick={() => dispatch({ type: 'SET_ZOOM', zoom: state.zoom * 1.33 })}
          >
            <ZoomIn className="w-4 h-4" />
          </Button>
        </div>
      </div>

      {/* Timeline Content */}
      <div className="flex flex-1 overflow-hidden">
        {/* Track Headers */}
        <div className="w-[200px] border-r border-white/5 bg-[#0E0E11] flex flex-col pt-6 overflow-y-auto shrink-0">
          {state.tracks.map(track => {
            const isVideo = track.type === 'video';
            return (
              <div
                key={track.id}
                className={`${isVideo ? 'h-20' : 'h-16'} border-b border-white/5 flex flex-col justify-center px-3 group hover:bg-white/[0.02]`}
              >
                <div className="flex items-center justify-between mb-2">
                  <div className="flex items-center gap-2 text-xs text-zinc-300 font-medium">
                    {isVideo
                      ? <Video className="w-3.5 h-3.5 text-blue-400" />
                      : <Music className="w-3.5 h-3.5 text-emerald-400" />
                    }
                    <span>{track.name}</span>
                  </div>
                  <Button
                    variant="ghost"
                    size="icon"
                    className="w-5 h-5 text-zinc-500 opacity-0 group-hover:opacity-100"
                  >
                    <MoreHorizontal className="w-3 h-3" />
                  </Button>
                </div>
                <div className="flex items-center gap-3">
                  <button
                    className="text-zinc-500 hover:text-zinc-300 transition-colors"
                    onClick={() => dispatch({ type: 'TOGGLE_TRACK_VISIBILITY', trackId: track.id })}
                    title={track.visible ? 'Hide track' : 'Show track'}
                  >
                    {track.visible
                      ? <Eye className="w-3.5 h-3.5" />
                      : <EyeOff className="w-3.5 h-3.5 text-zinc-600" />
                    }
                  </button>
                  <button
                    className="text-zinc-500 hover:text-zinc-300 transition-colors"
                    onClick={() => dispatch({ type: 'TOGGLE_TRACK_LOCK', trackId: track.id })}
                    title={track.locked ? 'Unlock track' : 'Lock track'}
                  >
                    {track.locked
                      ? <Lock className="w-3.5 h-3.5 text-amber-500" />
                      : <Unlock className="w-3.5 h-3.5" />
                    }
                  </button>
                </div>
              </div>
            );
          })}
        </div>

        {/* Timeline Tracks Scroll Area */}
        <div
          ref={scrollRef}
          className="flex-1 bg-[#121217] relative overflow-x-auto overflow-y-auto"
          onClick={(e) => {
            // Click on empty area → deselect
            if ((e.target as Element).closest('[data-clip]') === null) {
              dispatch({ type: 'SET_SELECTED_ITEM', id: null });
            }
          }}
        >
          <div className="relative" style={{ width: totalWidth, minHeight: '100%' }}>
            {/* Ruler */}
            <TimelineRuler zoom={state.zoom} duration={state.duration} onSeek={handleSeek} />

            {/* Playhead */}
            <Playhead currentTime={state.currentTime} zoom={state.zoom} onSeek={handleSeek} />

            {/* Track rows */}
            {state.tracks.map((track, trackIndex) => {
              const isVideo = track.type === 'video';
              const trackItems = state.items.filter(i => i.trackIndex === trackIndex);
              return (
                <div
                  key={track.id}
                  className={`${isVideo ? 'h-20' : 'h-16'} border-b border-white/5 relative ${
                    track.visible ? '' : 'opacity-30'
                  } ${track.locked ? 'pointer-events-none' : ''}`}
                >
                  {/* Empty track hint */}
                  {trackItems.length === 0 && (
                    <div className="absolute inset-0 flex items-center justify-center pointer-events-none">
                      <div className="text-xs text-zinc-700 flex items-center gap-2">
                        <ImageIcon className="w-3.5 h-3.5" />
                        <span>Drop media here</span>
                      </div>
                    </div>
                  )}

                  {/* Clips */}
                  {trackItems.map(item => {
                    const asset = state.assets.find(a => a.id === item.assetId);
                    return (
                      <div key={item.id} data-clip={item.id}>
                        <TimelineClip
                          item={item}
                          zoom={state.zoom}
                          isSelected={state.selectedItemId === item.id}
                          assetName={asset?.name ?? 'Unknown'}
                          assetType={asset?.type ?? 'video'}
                          onSelect={() => dispatch({ type: 'SET_SELECTED_ITEM', id: item.id })}
                          onMove={newStart => handleItemMove(item.id, newStart)}
                        />
                      </div>
                    );
                  })}
                </div>
              );
            })}

            {/* Empty state overlay */}
            {!hasItems && (
              <div className="absolute inset-0 top-6 flex items-center justify-center pointer-events-none">
                <div className="text-xs text-zinc-600 flex items-center gap-2">
                  <ImageIcon className="w-4 h-4" />
                  <span>Drag and drop media here to start editing</span>
                </div>
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
