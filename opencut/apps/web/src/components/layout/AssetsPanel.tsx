import React, { useRef, useState, useCallback } from 'react';
import {
  UploadCloud, Search, ArrowDownUp, Video, Image as ImageIcon,
  Music, Shapes, Folder, Mic, X, Film, FileAudio
} from 'lucide-react';
import { Button } from '../ui/button';
import { useEditor } from '../../store/editor-store';
import type { Asset } from '../../store/editor-store';

type FilterType = 'All' | 'Videos' | 'Photos' | 'Audio';

const filterButtons: FilterType[] = ['All', 'Videos', 'Photos', 'Audio'];

function AssetThumbnail({ asset, onAddToTimeline }: { asset: Asset; onAddToTimeline: (id: string) => void }) {
  const Icon = asset.type === 'video' ? Film : asset.type === 'audio' ? FileAudio : ImageIcon;
  return (
    <div
      className="relative bg-white/[0.03] hover:bg-white/[0.08] rounded-lg overflow-hidden cursor-pointer transition-colors border border-white/5 group aspect-video"
      onClick={() => onAddToTimeline(asset.id)}
      title={`${asset.name} — click to add to timeline`}
    >
      {asset.type === 'image' ? (
        <img src={asset.url} alt={asset.name} className="w-full h-full object-cover" />
      ) : asset.type === 'video' ? (
        <video src={asset.url} className="w-full h-full object-cover" muted preload="metadata" />
      ) : (
        <div className="w-full h-full flex items-center justify-center bg-emerald-500/10">
          <FileAudio className="w-8 h-8 text-emerald-400" />
        </div>
      )}
      {/* Overlay with name */}
      <div className="absolute inset-0 bg-black/40 opacity-0 group-hover:opacity-100 transition-opacity flex items-end p-1">
        <span className="text-[9px] text-white truncate leading-tight">{asset.name}</span>
      </div>
      {/* Duration badge */}
      {asset.duration && (
        <div className="absolute top-1 right-1 bg-black/70 text-[9px] text-white px-1 rounded">
          {Math.floor(asset.duration)}s
        </div>
      )}
      {/* Type indicator */}
      <div className="absolute top-1 left-1">
        <Icon className="w-3 h-3 text-white/70" />
      </div>
    </div>
  );
}

export function AssetsPanel() {
  const { state, dispatch, addAssetFromFile, addItemFromAsset } = useEditor();
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [isDraggingOver, setIsDraggingOver] = useState(false);
  const [filter, setFilter] = useState<FilterType>('All');
  const [searchQuery, setSearchQuery] = useState('');
  const [showSearch, setShowSearch] = useState(false);

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

  const filteredAssets = state.assets.filter(asset => {
    const matchesFilter =
      filter === 'All' ? true :
      filter === 'Videos' ? asset.type === 'video' :
      filter === 'Photos' ? asset.type === 'image' :
      filter === 'Audio' ? asset.type === 'audio' : true;
    const matchesSearch = !searchQuery || asset.name.toLowerCase().includes(searchQuery.toLowerCase());
    return matchesFilter && matchesSearch;
  });

  // Show panel only for Media / Audio tabs, or always show for now
  const isVisible = state.activeSidebarTab === 'Media' || state.activeSidebarTab === 'Audio';

  if (!isVisible) {
    return (
      <div className="w-[320px] border-r border-white/5 bg-[#121217] flex flex-col shrink-0 items-center justify-center">
        <p className="text-xs text-zinc-500 text-center px-6">
          Select <span className="text-zinc-300">Media</span> or <span className="text-zinc-300">Audio</span> in the sidebar to manage assets.
        </p>
      </div>
    );
  }

  return (
    <div
      className={`w-[320px] border-r border-white/5 bg-[#121217] flex flex-col shrink-0 transition-colors ${isDraggingOver ? 'bg-indigo-500/5 border-indigo-500/30' : ''}`}
      onDrop={onDrop}
      onDragOver={onDragOver}
      onDragLeave={onDragLeave}
    >
      {/* Header */}
      <div className="p-4 flex items-center justify-between border-b border-white/5">
        <h2 className="font-semibold text-sm">Assets</h2>
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
      </div>

      {/* Search bar */}
      {showSearch && (
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
        {/* Drop zone (always shown if no assets) */}
        {state.assets.length === 0 && (
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
              <AssetThumbnail key={asset.id} asset={asset} onAddToTimeline={handleAddToTimeline} />
            ))}
          </div>
        )}

        {/* Has assets but filter shows nothing */}
        {state.assets.length > 0 && filteredAssets.length === 0 && (
          <div className="flex flex-col items-center justify-center py-10 text-center">
            <p className="text-xs text-zinc-500">No {filter.toLowerCase()} found</p>
          </div>
        )}

        {/* Import more button when assets exist */}
        {state.assets.length > 0 && (
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
