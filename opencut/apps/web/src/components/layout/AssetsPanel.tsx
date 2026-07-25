import React, { useRef, useState, useCallback, useEffect } from 'react';
import {
  UploadCloud, Search, ArrowDownUp,
  Music, X, Film, FileAudio, Plus, Wand2, SlidersHorizontal, ArrowRightLeft, Type, Image as ImageIcon
} from 'lucide-react';
import { Button } from '../ui/button';
import { useEditor } from '../../store/editor-store';
import type { Asset } from '../../store/editor-store';

type FilterType = 'All' | 'Videos' | 'Photos' | 'Audio';

const filterButtons: FilterType[] = ['All', 'Videos', 'Photos', 'Audio'];

// ─── Stock Presets ────────────────────────────────────────────────────────────

const STOCK_AUDIO = [
  { id: 'stock-audio-1', name: 'Upbeat Corporate Beat', url: 'https://www.soundhelix.com/examples/mp3/SoundHelix-Song-1.mp3', duration: 30 },
  { id: 'stock-audio-2', name: 'Chill Lofi Mood', url: 'https://www.soundhelix.com/examples/mp3/SoundHelix-Song-2.mp3', duration: 45 },
  { id: 'stock-audio-3', name: 'Cinematic Ambient', url: 'https://www.soundhelix.com/examples/mp3/SoundHelix-Song-3.mp3', duration: 60 },
  { id: 'stock-audio-4', name: 'Inspiring Piano', url: 'https://www.soundhelix.com/examples/mp3/SoundHelix-Song-4.mp3', duration: 25 },
];

const STOCK_TEXT = [
  { id: 'stock-text-1', name: 'Basic Text', url: 'text://basic', duration: 5 },
  { id: 'stock-text-2', name: 'Neon Glow Title', url: 'text://neon', duration: 5 },
  { id: 'stock-text-3', name: 'Glitch Effect Title', url: 'text://glitch', duration: 5 },
  { id: 'stock-text-4', name: 'Bold Minimal Title', url: 'text://bold', duration: 5 },
];

const STOCK_ELEMENTS = [
  { id: 'stock-el-1', name: 'Red Circle', url: 'data:image/svg+xml,%3Csvg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100"%3E%3Ccircle cx="50" cy="50" r="40" fill="%23ef4444"/%3E%3C/svg%3E', duration: 5 },
  { id: 'stock-el-2', name: 'Yellow Star', url: 'data:image/svg+xml,%3Csvg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="%23eab308"%3E%3Cpolygon points="12,2 15,9 22,9 17,14 19,21 12,17 5,21 7,14 2,9 9,9"/%3E%3C/svg%3E', duration: 5 },
  { id: 'stock-el-3', name: 'Heart Sticker', url: 'data:image/svg+xml,%3Csvg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="%23ec4899"%3E%3Cpath d="M12,21.35L10.55,20.03C5.4,15.36 2,12.27 2,8.5C2,5.41 4.42,3 7.5,3C9.24,3 10.91,3.81 12,5.08C13.09,3.81 14.76,3 16.5,3C19.58,3 22,5.41 22,8.5C22,12.27 18.6,15.36 13.45,20.03L12,21.35Z"/%3E%3C/svg%3E', duration: 5 },
  { id: 'stock-el-4', name: 'Neon Arrow', url: 'data:image/svg+xml,%3Csvg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="%236366f1"%3E%3Cpath d="M4,15V9H12V3L21,12L12,21V15H4Z"/%3E%3C/svg%3E', duration: 5 },
];

const STOCK_TRANSITIONS = ['Fade', 'Dissolve', 'Zoom In', 'Zoom Out', 'Spin', 'Slide Up'];

const STOCK_EFFECTS = ['Blur', 'Glow', 'Retro', 'Vignette', 'Film Grain'];

// ─── Thumbnail Components ──────────────────────────────────────────────────────

function AssetThumbnail({
  asset,
  onAddToTimeline,
  onPreviewSource,
  isActivePreview
}: {
  asset: Asset;
  onAddToTimeline: (id: string) => void;
  onPreviewSource: (id: string) => void;
  isActivePreview: boolean;
}) {
  const Icon = asset.type === 'video' ? Film : asset.type === 'audio' ? FileAudio : ImageIcon;
  const videoRef = useRef<HTMLVideoElement>(null);

  const handleMouseEnter = () => {
    if (asset.type === 'video' && videoRef.current) {
      videoRef.current.play().catch(() => {});
    }
  };

  const handleMouseLeave = () => {
    if (asset.type === 'video' && videoRef.current) {
      videoRef.current.pause();
      videoRef.current.currentTime = 0;
    }
  };

  const handleDragStart = (e: React.DragEvent) => {
    e.dataTransfer.setData('text/plain', asset.id);
    e.dataTransfer.effectAllowed = 'copy';
  };

  return (
    <div
      draggable
      onDragStart={handleDragStart}
      onMouseEnter={handleMouseEnter}
      onMouseLeave={handleMouseLeave}
      onClick={() => onPreviewSource(asset.id)}
      className={`relative bg-white/[0.03] hover:bg-white/[0.08] rounded-lg overflow-hidden cursor-pointer transition-colors border ${
        isActivePreview ? 'border-indigo-500 ring-1 ring-indigo-500/50' : 'border-white/5'
      } group aspect-video select-none`}
      title={`${asset.name} — Click to preview`}
    >
      {asset.type === 'image' ? (
        <img src={asset.url} alt={asset.name} className="w-full h-full object-cover" />
      ) : asset.type === 'video' ? (
        <video
          ref={videoRef}
          src={asset.url}
          className="w-full h-full object-cover pointer-events-none"
          muted
          loop
          preload="metadata"
        />
      ) : (
        <div className="w-full h-full flex items-center justify-center bg-emerald-500/10">
          <FileAudio className="w-8 h-8 text-emerald-400" />
        </div>
      )}

      {/* Hover CapCut-style + Overlay */}
      <div className="absolute inset-0 bg-black/50 opacity-0 group-hover:opacity-100 transition-opacity flex items-center justify-center gap-2 z-10">
        <button
          onClick={(e) => {
            e.stopPropagation();
            onAddToTimeline(asset.id);
          }}
          className="w-8 h-8 bg-indigo-500 hover:bg-indigo-600 text-white rounded-full flex items-center justify-center shadow-lg transition-transform hover:scale-110 active:scale-95"
          title="Add to timeline"
        >
          <Plus className="w-4 h-4" />
        </button>
      </div>

      {/* Overlay with name */}
      <div className="absolute inset-x-0 bottom-0 bg-gradient-to-t from-black/80 to-transparent p-1 pt-4 opacity-100 group-hover:opacity-0 transition-opacity pointer-events-none">
        <p className="text-[9px] text-white/90 truncate leading-none">{asset.name}</p>
      </div>
      {/* Duration badge */}
      {asset.duration && (
        <div className="absolute top-1 right-1 bg-black/70 text-[9px] text-white px-1 rounded pointer-events-none">
          {Math.floor(asset.duration)}s
        </div>
      )}
      {/* Type indicator */}
      <div className="absolute top-1 left-1 bg-black/40 p-0.5 rounded pointer-events-none">
        <Icon className="w-3 h-3 text-white/70" />
      </div>
    </div>
  );
}

// ─── Main Assets Panel ─────────────────────────────────────────────────────────

export function AssetsPanel() {
  const { state, dispatch, addAssetFromFile, addItemFromAsset } = useEditor();
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [isDraggingOver, setIsDraggingOver] = useState(false);
  const [filter, setFilter] = useState<FilterType>('All');
  const [searchQuery, setSearchQuery] = useState('');
  const [showSearch, setShowSearch] = useState(false);

  // Audio preview playback states
  const [playingAudioUrl, setPlayingAudioUrl] = useState<string | null>(null);
  const audioPreviewRef = useRef<HTMLAudioElement | null>(null);

  // Clean up audio playback on unmount
  useEffect(() => {
    return () => {
      if (audioPreviewRef.current) {
        audioPreviewRef.current.pause();
        audioPreviewRef.current = null;
      }
    };
  }, []);

  const toggleAudioPreview = (url: string) => {
    if (playingAudioUrl === url) {
      audioPreviewRef.current?.pause();
      setPlayingAudioUrl(null);
    } else {
      if (!audioPreviewRef.current) {
        audioPreviewRef.current = new Audio(url);
      } else {
        audioPreviewRef.current.src = url;
      }
      audioPreviewRef.current.play().catch(() => {});
      setPlayingAudioUrl(url);
    }
  };

  const handleFiles = useCallback((files: FileList | null) => {
    if (!files) return;
    Array.from(files).forEach(file => {
      if (file.type.startsWith('video/') || file.type.startsWith('image/') || file.type.startsWith('audio/')) {
        addAssetFromFile(file);
      }
    });
  }, [addAssetFromFile]);

  const onDrop = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    setIsDraggingOver(false);
    handleFiles(e.dataTransfer.files);
  }, [handleFiles]);

  const onDragOver = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    setIsDraggingOver(true);
  }, []);

  const onDragLeave = useCallback(() => {
    setIsDraggingOver(false);
  }, []);

  const getOrCreateStockAsset = useCallback((name: string, type: 'audio' | 'text' | 'image', url: string, duration: number = 5) => {
    let existing = state.assets.find(a => a.url === url || (type === 'text' && a.name === name));
    if (existing) return existing.id;
    const id = `stock-${type}-${Date.now()}-${Math.random().toString(36).slice(2, 6)}`;
    const asset: Asset = {
      id,
      name,
      type,
      url,
      duration,
      file: new File([], name),
    };
    dispatch({ type: 'ADD_ASSET', asset });
    return id;
  }, [state.assets, dispatch]);

  const handleAddToTimeline = useCallback((assetId: string) => {
    const asset = state.assets.find(a => a.id === assetId);
    if (!asset) return;
    const targetTrack = state.tracks.find(t =>
      (asset.type === 'audio' ? t.type === 'audio' : t.type === 'video')
    );
    if (targetTrack) {
      addItemFromAsset(assetId, targetTrack.id);
    }
  }, [state.assets, state.tracks, addItemFromAsset]);

  const handlePreviewSource = useCallback((assetId: string) => {
    dispatch({ type: 'SET_PREVIEW_ASSET', id: assetId });
  }, [dispatch]);

  const filteredAssets = state.assets.filter(asset => {
    // Only display user-uploaded assets in the Media tab
    if (asset.id.startsWith('stock-')) return false;
    const matchesFilter =
      filter === 'All' ? true :
      filter === 'Videos' ? asset.type === 'video' :
      filter === 'Photos' ? asset.type === 'image' :
      filter === 'Audio' ? asset.type === 'audio' : true;
    const matchesSearch = !searchQuery || asset.name.toLowerCase().includes(searchQuery.toLowerCase());
    return matchesFilter && matchesSearch;
  });

  const selectedItem = state.items.find(i => i.id === state.selectedItemId) ?? null;

  // ─── Tab Rendering ───

  const renderContent = () => {
    const tab = state.activeSidebarTab;

    if (tab === 'Media') {
      return (
        <div className="flex flex-col h-full overflow-hidden">
          {/* Filter tabs */}
          <div className="flex items-center px-4 gap-1 py-2 border-b border-white/5">
            {filterButtons.map(f => (
              <button
                key={f}
                onClick={() => setFilter(f)}
                className={`text-[10px] px-2 py-1 rounded-md transition-colors ${
                  filter === f
                    ? 'bg-indigo-500/20 text-indigo-400'
                    : 'text-zinc-500 hover:text-zinc-300 hover:bg-white/5'
                }`}
              >
                {f}
              </button>
            ))}
          </div>

          <div className="p-4 flex-1 overflow-y-auto">
            {/* Drop zone */}
            {filteredAssets.length === 0 && (
              <div
                className={`border border-dashed rounded-xl p-8 flex flex-col items-center justify-center text-center mb-6 transition-colors cursor-pointer ${
                  isDraggingOver
                    ? 'border-indigo-500/60 bg-indigo-500/10'
                    : 'border-white/10 bg-white/[0.02] hover:border-white/20'
                }`}
                onClick={() => fileInputRef.current?.click()}
              >
                <UploadCloud className={`w-8 h-8 mb-3 ${isDraggingOver ? 'text-indigo-400' : 'text-zinc-400'}`} />
                <p className="text-xs text-zinc-300 mb-1">Drag and drop media here</p>
                <p className="text-[10px] text-zinc-500 mb-4">or</p>
                <Button variant="secondary" className="h-8 text-xs bg-white/10 hover:bg-white/20 text-white border-0">
                  Browse files
                </Button>
              </div>
            )}

            {/* Asset grid */}
            {filteredAssets.length > 0 && (
              <div className="grid grid-cols-2 gap-2">
                {filteredAssets.map(asset => (
                  <AssetThumbnail
                    key={asset.id}
                    asset={asset}
                    onAddToTimeline={handleAddToTimeline}
                    onPreviewSource={handlePreviewSource}
                    isActivePreview={state.previewAssetId === asset.id}
                  />
                ))}
              </div>
            )}

            {filteredAssets.length > 0 && (
              <button
                className="mt-4 w-full border border-dashed border-white/10 rounded-lg py-3 text-xs text-zinc-500 hover:border-white/20 hover:text-zinc-300 transition-colors flex items-center justify-center gap-2"
                onClick={() => fileInputRef.current?.click()}
              >
                <UploadCloud className="w-3.5 h-3.5" />
                Import more
              </button>
            )}
          </div>
        </div>
      );
    }

    if (tab === 'Audio') {
      return (
        <div className="p-4 flex-1 overflow-y-auto space-y-2">
          <span className="text-[10px] text-zinc-500 font-semibold block mb-2 uppercase tracking-wider">Stock Audio Tracks</span>
          {STOCK_AUDIO.map(stock => {
            const isPlaying = playingAudioUrl === stock.url;
            return (
              <div
                key={stock.id}
                draggable
                onDragStart={(e) => {
                  const assetId = getOrCreateStockAsset(stock.name, 'audio', stock.url, stock.duration);
                  e.dataTransfer.setData('text/plain', assetId);
                }}
                className="flex items-center justify-between p-2.5 bg-white/[0.02] hover:bg-white/[0.06] border border-white/5 rounded-lg transition-colors group select-none cursor-grab"
              >
                <div className="flex items-center gap-2.5 min-w-0">
                  <button
                    onClick={() => toggleAudioPreview(stock.url)}
                    className="w-8 h-8 rounded-full bg-indigo-500/10 hover:bg-indigo-500/20 text-indigo-400 flex items-center justify-center shrink-0"
                  >
                    <Music className={`w-4 h-4 ${isPlaying ? 'animate-bounce text-indigo-400' : 'text-zinc-400'}`} />
                  </button>
                  <div className="min-w-0 text-left">
                    <p className="text-xs text-zinc-300 truncate font-medium">{stock.name}</p>
                    <p className="text-[10px] text-zinc-500">{stock.duration}s</p>
                  </div>
                </div>
                <button
                  onClick={() => {
                    const assetId = getOrCreateStockAsset(stock.name, 'audio', stock.url, stock.duration);
                    handleAddToTimeline(assetId);
                  }}
                  className="w-6 h-6 rounded-full bg-white/5 hover:bg-indigo-500 hover:text-white text-zinc-400 flex items-center justify-center shrink-0 opacity-0 group-hover:opacity-100 transition-all hover:scale-105"
                  title="Add to timeline"
                >
                  <Plus className="w-3.5 h-3.5" />
                </button>
              </div>
            );
          })}
        </div>
      );
    }

    if (tab === 'Text') {
      return (
        <div className="p-4 flex-1 overflow-y-auto space-y-2">
          <span className="text-[10px] text-zinc-500 font-semibold block mb-2 uppercase tracking-wider">Text Presets</span>
          {STOCK_TEXT.map(preset => (
            <div
              key={preset.id}
              draggable
              onDragStart={(e) => {
                const assetId = getOrCreateStockAsset(preset.name, 'text', preset.url, preset.duration);
                e.dataTransfer.setData('text/plain', assetId);
              }}
              className="flex items-center justify-between p-3 bg-white/[0.02] hover:bg-white/[0.06] border border-white/5 rounded-lg transition-colors group select-none cursor-grab"
            >
              <div className="flex items-center gap-2">
                <Type className="w-4 h-4 text-indigo-400" />
                <span className="text-xs text-zinc-300 font-medium">{preset.name}</span>
              </div>
              <button
                onClick={() => {
                  const assetId = getOrCreateStockAsset(preset.name, 'text', preset.url, preset.duration);
                  handleAddToTimeline(assetId);
                }}
                className="w-6 h-6 rounded-full bg-white/5 hover:bg-indigo-500 hover:text-white text-zinc-400 flex items-center justify-center shrink-0 opacity-0 group-hover:opacity-100 transition-all hover:scale-105"
                title="Add text to timeline"
              >
                <Plus className="w-3.5 h-3.5" />
              </button>
            </div>
          ))}
        </div>
      );
    }

    if (tab === 'Elements') {
      return (
        <div className="p-4 flex-1 overflow-y-auto space-y-2">
          <span className="text-[10px] text-zinc-500 font-semibold block mb-2 uppercase tracking-wider">Graphics & Stickers</span>
          <div className="grid grid-cols-2 gap-2">
            {STOCK_ELEMENTS.map(preset => (
              <div
                key={preset.id}
                draggable
                onDragStart={(e) => {
                  const assetId = getOrCreateStockAsset(preset.name, 'image', preset.url, preset.duration);
                  e.dataTransfer.setData('text/plain', assetId);
                }}
                className="relative bg-white/[0.02] hover:bg-white/[0.06] border border-white/5 rounded-lg p-3 flex flex-col items-center justify-center group aspect-square select-none cursor-grab"
              >
                <img src={preset.url} alt="" className="w-12 h-12 object-contain pointer-events-none mb-1.5" />
                <span className="text-[9px] text-zinc-400 text-center font-medium truncate w-full">{preset.name}</span>
                <button
                  onClick={() => {
                    const assetId = getOrCreateStockAsset(preset.name, 'image', preset.url, preset.duration);
                    handleAddToTimeline(assetId);
                  }}
                  className="absolute inset-0 bg-black/40 opacity-0 group-hover:opacity-100 transition-opacity flex items-center justify-center"
                >
                  <div className="w-7 h-7 rounded-full bg-indigo-500 text-white flex items-center justify-center shadow-lg transition-transform hover:scale-105">
                    <Plus className="w-4 h-4" />
                  </div>
                </button>
              </div>
            ))}
          </div>
        </div>
      );
    }

    if (tab === 'Transitions') {
      return (
        <div className="p-4 flex-1 overflow-y-auto">
          <span className="text-[10px] text-zinc-500 font-semibold block mb-1 uppercase tracking-wider">Visual Transitions</span>
          <p className="text-[10px] text-zinc-600 mb-3 text-left">Select a clip on the timeline to apply a transition</p>
          <div className="grid grid-cols-2 gap-2">
            {STOCK_TRANSITIONS.map(name => {
              const isActive = selectedItem?.transition === name;
              return (
                <div
                  key={name}
                  onClick={() => {
                    if (selectedItem) {
                      dispatch({
                        type: 'UPDATE_ITEM',
                        id: selectedItem.id,
                        updates: { transition: isActive ? undefined : name }
                      });
                    }
                  }}
                  className={`border p-3 rounded-lg text-center cursor-pointer transition-all select-none hover:bg-white/[0.06] ${
                    isActive
                      ? 'border-indigo-500 bg-indigo-500/10 text-indigo-400'
                      : 'border-white/5 bg-white/[0.02] text-zinc-400 hover:text-zinc-200'
                  }`}
                >
                  <div className="w-full h-8 bg-white/5 rounded mb-1.5 flex items-center justify-center">
                    <ArrowRightLeft className={`w-4 h-4 ${isActive ? 'text-indigo-400' : 'text-zinc-600'}`} />
                  </div>
                  <span className="text-[10px] font-medium block">{name}</span>
                </div>
              );
            })}
          </div>
        </div>
      );
    }

    if (tab === 'Effects') {
      return (
        <div className="p-4 flex-1 overflow-y-auto">
          <span className="text-[10px] text-zinc-500 font-semibold block mb-1 uppercase tracking-wider">Video Filters</span>
          <p className="text-[10px] text-zinc-600 mb-3 text-left">Select a clip on the timeline to apply an effect</p>
          <div className="grid grid-cols-2 gap-2">
            {STOCK_EFFECTS.map(name => {
              const isActive = selectedItem?.effect === name;
              return (
                <div
                  key={name}
                  onClick={() => {
                    if (selectedItem) {
                      dispatch({
                        type: 'UPDATE_ITEM',
                        id: selectedItem.id,
                        updates: { effect: isActive ? undefined : name }
                      });
                    }
                  }}
                  className={`border p-3 rounded-lg text-center cursor-pointer transition-all select-none hover:bg-white/[0.06] ${
                    isActive
                      ? 'border-indigo-500 bg-indigo-500/10 text-indigo-400'
                      : 'border-white/5 bg-white/[0.02] text-zinc-400 hover:text-zinc-200'
                  }`}
                >
                  <div className="w-full h-8 bg-white/5 rounded mb-1.5 flex items-center justify-center">
                    <Wand2 className={`w-4 h-4 ${isActive ? 'text-indigo-400' : 'text-zinc-600'}`} />
                  </div>
                  <span className="text-[10px] font-medium block">{name}</span>
                </div>
              );
            })}
          </div>
        </div>
      );
    }

    if (tab === 'Adjust') {
      return (
        <div className="p-4 flex-1 overflow-y-auto">
          <span className="text-[10px] text-zinc-500 font-semibold block mb-1 uppercase tracking-wider">Live Color Grading</span>
          <p className="text-[10px] text-zinc-600 mb-4 text-left">Adjust the values for the selected clip</p>
          {selectedItem ? (
            <div className="space-y-4 pt-2">
              <div className="flex flex-col gap-1">
                <div className="flex items-center justify-between text-xs text-zinc-400">
                  <span>Opacity</span>
                  <span>{Math.round(selectedItem.opacity * 100)}%</span>
                </div>
                <input
                  type="range"
                  min="0"
                  max="100"
                  value={Math.round(selectedItem.opacity * 100)}
                  onChange={e => dispatch({ type: 'UPDATE_ITEM', id: selectedItem.id, updates: { opacity: parseFloat(e.target.value) / 100 } })}
                  className="w-full accent-indigo-500 bg-white/10 rounded-lg appearance-none h-1"
                />
              </div>

              <div className="flex flex-col gap-1">
                <div className="flex items-center justify-between text-xs text-zinc-400">
                  <span>Rotation</span>
                  <span>{Math.round(selectedItem.rotation)}°</span>
                </div>
                <input
                  type="range"
                  min="-360"
                  max="360"
                  value={Math.round(selectedItem.rotation)}
                  onChange={e => dispatch({ type: 'UPDATE_ITEM', id: selectedItem.id, updates: { rotation: parseInt(e.target.value) } })}
                  className="w-full accent-indigo-500 bg-white/10 rounded-lg appearance-none h-1"
                />
              </div>

              <div className="flex flex-col gap-1">
                <div className="flex items-center justify-between text-xs text-zinc-400">
                  <span>Scale (X)</span>
                  <span>{Math.round(selectedItem.scaleX * 100)}%</span>
                </div>
                <input
                  type="range"
                  min="10"
                  max="300"
                  value={Math.round(selectedItem.scaleX * 100)}
                  onChange={e => dispatch({ type: 'UPDATE_ITEM', id: selectedItem.id, updates: { scaleX: parseFloat(e.target.value) / 100, scaleY: parseFloat(e.target.value) / 100 } })}
                  className="w-full accent-indigo-500 bg-white/10 rounded-lg appearance-none h-1"
                />
              </div>
              <Button
                variant="outline"
                className="w-full mt-2 border-white/10 text-xs text-zinc-400 hover:text-white"
                onClick={() => dispatch({ type: 'UPDATE_ITEM', id: selectedItem.id, updates: { x: 0, y: 0, scaleX: 1, scaleY: 1, rotation: 0, opacity: 1 } })}
              >
                Reset Adjustments
              </Button>
            </div>
          ) : (
            <div className="flex flex-col items-center justify-center py-10 text-center opacity-40">
              <SlidersHorizontal className="w-10 h-10 text-zinc-400 mb-2" />
              <p className="text-xs text-zinc-500 px-6">Select a clip on the timeline to unlock direct adjustments.</p>
            </div>
          )}
        </div>
      );
    }

    return (
      <div className="w-[320px] flex-1 flex flex-col items-center justify-center p-6 text-center text-zinc-500">
        <p className="text-xs">Tab "{tab}" is functionally loaded.</p>
      </div>
    );
  };

  return (
    <div
      className={`w-[320px] border-r border-white/5 bg-[#121217] flex flex-col shrink-0 transition-colors ${
        state.activeSidebarTab === 'Media' && isDraggingOver ? 'bg-indigo-500/5 border-indigo-500/30' : ''
      }`}
      onDrop={state.activeSidebarTab === 'Media' ? onDrop : undefined}
      onDragOver={state.activeSidebarTab === 'Media' ? onDragOver : undefined}
      onDragLeave={state.activeSidebarTab === 'Media' ? onDragLeave : undefined}
    >
      {/* Header */}
      <div className="p-4 flex items-center justify-between border-b border-white/5">
        <h2 className="font-semibold text-sm capitalize">{state.activeSidebarTab}</h2>
        {state.activeSidebarTab === 'Media' && (
          <div className="flex items-center gap-2">
            <Button
              variant="ghost"
              size="icon"
              className="w-7 h-7 text-zinc-400 hover:text-white"
              onClick={() => setShowSearch(v => !v)}
              title="Search assets"
            >
              <Search className="w-4 h-4" />
            </Button>
            <Button
              variant="ghost"
              size="icon"
              className="w-7 h-7 text-zinc-400 hover:text-white"
              title="Sort assets"
            >
              <ArrowDownUp className="w-4 h-4" />
            </Button>
            <Button
              className="h-7 text-xs bg-indigo-500 hover:bg-indigo-600 px-3"
              onClick={() => fileInputRef.current?.click()}
            >
              Import
            </Button>
            <input
              ref={fileInputRef}
              type="file"
              accept="video/*,image/*,audio/*"
              multiple
              className="hidden"
              onChange={e => handleFiles(e.target.files)}
            />
          </div>
        )}
      </div>

      {/* Search bar */}
      {state.activeSidebarTab === 'Media' && showSearch && (
        <div className="px-4 py-2 border-b border-white/5 flex items-center gap-2">
          <Search className="w-3.5 h-3.5 text-zinc-500 shrink-0" />
          <input
            type="text"
            placeholder="Search assets…"
            className="flex-1 bg-transparent text-xs text-zinc-300 outline-none placeholder:text-zinc-600"
            value={searchQuery}
            onChange={e => setSearchQuery(e.target.value)}
            autoFocus
          />
          {searchQuery && (
            <button onClick={() => setSearchQuery('')} className="text-zinc-500 hover:text-zinc-300">
              <X className="w-3.5 h-3.5" />
            </button>
          )}
        </div>
      )}

      {/* Main Tab Content */}
      {renderContent()}
    </div>
  );
}
