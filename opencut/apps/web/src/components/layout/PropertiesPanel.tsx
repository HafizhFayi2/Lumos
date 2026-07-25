import React, { useState, useCallback } from 'react';
import { Sparkles, RotateCw } from 'lucide-react';
import { Button } from '../ui/button';
import { useEditor } from '../../store/editor-store';
import type { PropertiesTab, TrackItem } from '../../store/editor-store';

const TABS: PropertiesTab[] = ['Properties', 'Adjust', 'Effects', 'Transitions'];

// A draggable number input
function NumberInput({
  label,
  value,
  unit = '',
  onChange,
  min,
  max,
  step = 1,
}: {
  label: string;
  value: number;
  unit?: string;
  onChange: (v: number) => void;
  min?: number;
  max?: number;
  step?: number;
}) {
  const [editing, setEditing] = useState(false);
  const [raw, setRaw] = useState('');

  return editing ? (
    <input
      className="flex items-center bg-black/60 rounded px-2 py-1 w-16 border border-indigo-500/50 text-xs text-zinc-100 outline-none"
      autoFocus
      type="number"
      value={raw}
      min={min}
      max={max}
      step={step}
      onChange={e => setRaw(e.target.value)}
      onBlur={() => {
        const v = parseFloat(raw);
        if (!isNaN(v)) onChange(min !== undefined ? Math.max(min, max !== undefined ? Math.min(max, v) : v) : v);
        setEditing(false);
      }}
      onKeyDown={e => {
        if (e.key === 'Enter') e.currentTarget.blur();
        if (e.key === 'Escape') setEditing(false);
      }}
    />
  ) : (
    <div
      className="flex items-center bg-black/40 rounded px-2 py-1 w-16 border border-white/5 cursor-text hover:border-white/20 transition-colors"
      onClick={() => { setRaw(String(value)); setEditing(true); }}
      title="Click to edit"
    >
      <span className="text-[10px] text-zinc-500 mr-1">{label}</span>
      <span className="text-xs text-zinc-300">{value}{unit}</span>
    </div>
  );
}

// Draggable opacity slider
function OpacitySlider({ value, onChange }: { value: number; onChange: (v: number) => void }) {
  const trackRef = React.useRef<HTMLDivElement>(null);
  const dragging = React.useRef(false);

  function getPercent(e: MouseEvent | React.MouseEvent) {
    const rect = trackRef.current?.getBoundingClientRect();
    if (!rect) return value;
    const pct = Math.max(0, Math.min(1, (e.clientX - rect.left) / rect.width));
    return Math.round(pct * 100);
  }

  function onMouseDown(e: React.MouseEvent) {
    dragging.current = true;
    onChange(getPercent(e));
    function onMove(ev: MouseEvent) { if (dragging.current) onChange(getPercent(ev)); }
    function onUp() { dragging.current = false; window.removeEventListener('mousemove', onMove); window.removeEventListener('mouseup', onUp); }
    window.addEventListener('mousemove', onMove);
    window.addEventListener('mouseup', onUp);
  }

  return (
    <div className="flex items-center gap-3">
      <div
        ref={trackRef}
        className="w-24 h-2 bg-white/10 rounded-full relative cursor-pointer"
        onMouseDown={onMouseDown}
      >
        <div
          className="absolute left-0 top-0 bottom-0 bg-indigo-500 rounded-full"
          style={{ width: `${value}%` }}
        />
        <div
          className="absolute top-1/2 -translate-y-1/2 w-3 h-3 bg-indigo-400 rounded-full shadow border border-white/20"
          style={{ left: `calc(${value}% - 6px)` }}
        />
      </div>
      <span className="text-xs text-zinc-300 w-8 text-right">{value}%</span>
    </div>
  );
}

function TransformSection({ item }: { item: TrackItem }) {
  const { state, dispatch } = useEditor();
  const update = useCallback((updates: Partial<TrackItem>) => {
    dispatch({ type: 'UPDATE_ITEM', id: item.id, updates });
  }, [dispatch, item.id]);

  const resetTransform = () => {
    update({ x: 0, y: 0, scaleX: 1, scaleY: 1, rotation: 0, opacity: 1 });
  };

  const asset = state.assets.find(a => a.id === item.assetId);
  const isPortrait = asset && asset.width && asset.height && asset.height > asset.width;

  return (
    <div className="mb-6">
      <div className="flex items-center justify-between mb-4">
        <h4 className="text-xs font-semibold text-zinc-300">Transform</h4>
        <Button
          variant="ghost"
          size="icon"
          className="w-5 h-5 text-zinc-500 hover:text-zinc-300"
          onClick={resetTransform}
          title="Reset transform"
        >
          <RotateCw className="w-3 h-3" />
        </Button>
      </div>

      <div className="space-y-3">
        {/* Position */}
        <div className="flex items-center justify-between">
          <span className="text-xs text-zinc-400">Position</span>
          <div className="flex gap-2">
            <NumberInput label="X" value={Math.round(item.x)} onChange={v => update({ x: v })} />
            <NumberInput label="Y" value={Math.round(item.y)} onChange={v => update({ y: v })} />
          </div>
        </div>

        {/* Scale */}
        <div className="flex items-center justify-between">
          <span className="text-xs text-zinc-400">Scale</span>
          <div className="flex gap-2">
            <NumberInput label="X" value={Math.round(item.scaleX * 100)} unit="%" onChange={v => update({ scaleX: v / 100 })} min={1} max={500} />
            <NumberInput label="Y" value={Math.round(item.scaleY * 100)} unit="%" onChange={v => update({ scaleY: v / 100 })} min={1} max={500} />
          </div>
        </div>

        {/* Rotation */}
        <div className="flex items-center justify-between">
          <span className="text-xs text-zinc-400">Rotation</span>
          <NumberInput label="" value={Math.round(item.rotation)} unit="°" onChange={v => update({ rotation: v })} min={-360} max={360} />
        </div>

        {/* Opacity */}
        <div className="flex items-center justify-between pt-2">
          <span className="text-xs text-zinc-400">Opacity</span>
          <OpacitySlider value={Math.round(item.opacity * 100)} onChange={v => update({ opacity: v / 100 })} />
        </div>

        {/* Portrait to Landscape Helpers */}
        {isPortrait && (
          <div className="mt-4 pt-4 border-t border-white/5">
            <span className="text-[10px] text-indigo-400 font-semibold uppercase tracking-wider block mb-2">
              Portrait to Landscape Helpers
            </span>
            <div className="flex gap-1.5">
              <Button
                variant="secondary"
                className="text-[10px] h-7 px-2 flex-1 bg-white/5 hover:bg-white/10 text-zinc-300 font-medium"
                onClick={() => {
                  if (asset.width && asset.height) {
                    const fillScale = (16 / 9) / (asset.width / asset.height);
                    update({ scaleX: fillScale, scaleY: fillScale, rotation: 0, x: 0, y: 0 });
                  }
                }}
                title="Scale to fill entire frame"
              >
                Fill Frame
              </Button>
              <Button
                variant="secondary"
                className="text-[10px] h-7 px-2 flex-1 bg-white/5 hover:bg-white/10 text-zinc-300 font-medium"
                onClick={() => update({ rotation: 90, scaleX: 1, scaleY: 1, x: 0, y: 0 })}
                title="Rotate 90 degrees"
              >
                Rotate 90°
              </Button>
              <Button
                variant="secondary"
                className="text-[10px] h-7 px-2 flex-1 bg-white/5 hover:bg-white/10 text-zinc-300 font-medium"
                onClick={() => update({ scaleX: 1, scaleY: 1, rotation: 0, x: 0, y: 0 })}
                title="Fit video in center with sidebars"
              >
                Fit (Bars)
              </Button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}

export function PropertiesPanel() {
  const { state, dispatch } = useEditor();
  const selectedItem = state.items.find(i => i.id === state.selectedItemId) ?? null;
  const selectedAsset = selectedItem ? state.assets.find(a => a.id === selectedItem.assetId) : null;

  return (
    <div className="w-[300px] border-l border-white/5 bg-[#121217] flex flex-col shrink-0">
      {/* Tabs */}
      <div className="flex items-center px-4 border-b border-white/5 h-12">
        {TABS.map((tab) => (
          <div
            key={tab}
            className={`text-xs font-medium px-3 h-full flex items-center border-b-2 cursor-pointer transition-colors ${
              state.activePropertiesTab === tab
                ? 'border-indigo-500 text-indigo-400'
                : 'border-transparent text-zinc-400 hover:text-zinc-200'
            }`}
            onClick={() => dispatch({ type: 'SET_PROPERTIES_TAB', tab })}
          >
            {tab}
          </div>
        ))}
      </div>

      <div className="flex-1 overflow-y-auto p-4">
        {/* No selection state */}
        {!selectedItem && (
          <div className="flex flex-col items-center justify-center py-10 text-center border-b border-white/5 mb-6 pb-8">
            <div className="w-12 h-12 bg-white/5 rounded-xl flex items-center justify-center mb-4">
              <Sparkles className="w-6 h-6 text-zinc-400" />
            </div>
            <h3 className="text-sm font-medium text-zinc-200 mb-2">Nothing selected</h3>
            <p className="text-xs text-zinc-500 leading-relaxed max-w-[200px]">
              Select an element on the timeline to view and edit its properties.
            </p>
          </div>
        )}

        {/* Selected item header */}
        {selectedItem && selectedAsset && (
          <div className="mb-4 pb-4 border-b border-white/5">
            <div className="flex items-center gap-2">
              <div className="w-8 h-8 bg-white/5 rounded flex items-center justify-center shrink-0">
                {selectedAsset.type === 'video' && <span className="text-[10px] text-blue-400">VID</span>}
                {selectedAsset.type === 'image' && <span className="text-[10px] text-purple-400">IMG</span>}
                {selectedAsset.type === 'audio' && <span className="text-[10px] text-emerald-400">AUD</span>}
                {selectedAsset.type === 'text' && <span className="text-[10px] text-indigo-400">TXT</span>}
              </div>
              <div className="flex-1 min-w-0">
                <p className="text-xs font-medium text-zinc-200 truncate">{selectedAsset.name}</p>
                <p className="text-[10px] text-zinc-500">{selectedItem.duration.toFixed(1)}s</p>
              </div>
            </div>
            {selectedAsset.type === 'text' && (
              <div className="mt-4 bg-black/20 p-3 rounded-lg border border-white/5">
                <span className="text-[10px] text-zinc-500 font-medium block mb-1.5">Text Field</span>
                <textarea
                  className="w-full bg-black/40 border border-white/10 rounded px-2.5 py-1.5 text-xs text-zinc-200 outline-none focus:border-indigo-500/50 resize-none h-16 transition-colors"
                  value={selectedAsset.name}
                  onChange={e => dispatch({ type: 'UPDATE_ASSET_NAME', id: selectedAsset.id, name: e.target.value })}
                  placeholder="Enter text..."
                />
              </div>
            )}
          </div>
        )}

        {/* Properties tab */}
        {state.activePropertiesTab === 'Properties' && selectedItem && (
          <TransformSection item={selectedItem} />
        )}

        {/* Adjust tab */}
        {state.activePropertiesTab === 'Adjust' && (
          <div className="space-y-4">
            {['Brightness', 'Contrast', 'Saturation', 'Sharpness'].map((name, i) => {
              const defaults = [50, 50, 50, 0];
              return (
                <div key={name} className="flex items-center justify-between">
                  <span className="text-xs text-zinc-400 w-20">{name}</span>
                  <div className="flex items-center gap-3">
                    <div className="w-24 h-1.5 bg-white/10 rounded-full relative cursor-pointer">
                      <div className="absolute left-0 top-0 bottom-0 bg-indigo-500 rounded-full" style={{ width: `${defaults[i]}%` }} />
                      <div className="absolute top-1/2 -translate-y-1/2 w-3 h-3 bg-indigo-400 rounded-full shadow border border-white/20" style={{ left: `calc(${defaults[i]}% - 6px)` }} />
                    </div>
                    <span className="text-xs text-zinc-400 w-8 text-right">{defaults[i]}</span>
                  </div>
                </div>
              );
            })}
          </div>
        )}

        {/* Effects tab */}
        {state.activePropertiesTab === 'Effects' && (
          <div className="space-y-2">
            {['Blur', 'Glow', 'Shadow', 'Vignette', 'Film Grain'].map(effect => (
              <div key={effect} className="flex items-center justify-between py-2 border-b border-white/5">
                <span className="text-xs text-zinc-300">{effect}</span>
                <div className="w-8 h-4 bg-white/10 rounded-full relative cursor-pointer hover:bg-white/20 transition-colors">
                  <div className="absolute left-0.5 top-0.5 w-3 h-3 bg-zinc-400 rounded-full" />
                </div>
              </div>
            ))}
          </div>
        )}

        {/* Transitions tab */}
        {state.activePropertiesTab === 'Transitions' && (
          <div className="grid grid-cols-2 gap-2">
            {['Fade', 'Dissolve', 'Wipe Left', 'Wipe Right', 'Zoom In', 'Zoom Out', 'Spin', 'Slide Up'].map(t => (
              <div
                key={t}
                className="bg-white/[0.03] hover:bg-white/[0.08] border border-white/5 rounded-lg p-3 cursor-pointer transition-colors text-center"
              >
                <div className="w-full h-8 bg-white/5 rounded mb-2 flex items-center justify-center">
                  <div className="w-4 h-4 bg-indigo-500/30 rounded-sm" />
                </div>
                <span className="text-[10px] text-zinc-400">{t}</span>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
