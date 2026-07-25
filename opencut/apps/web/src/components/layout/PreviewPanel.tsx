import React, { useRef, useState, useCallback, useEffect } from 'react';
import {
  Play, Pause, SkipBack, SkipForward, Maximize, Minimize,
  ChevronDown
} from 'lucide-react';
import { Button } from '../ui/button';
import { useEditor, formatTime } from '../../store/editor-store';

const FIT_OPTIONS = ['Fit', '25%', '50%', '75%', '100%'];

function getAspectRatioDims(ratio: string): [number, number] {
  if (ratio === '9:16') return [9, 16];
  if (ratio === '1:1') return [1, 1];
  if (ratio === '4:3') return [4, 3];
  return [16, 9];
}

export function PreviewPanel() {
  const { state, dispatch, togglePlay } = useEditor();
  const containerRef = useRef<HTMLDivElement>(null);
  const canvasRef = useRef<HTMLDivElement>(null);
  const videoRef = useRef<HTMLVideoElement>(null);
  const [isFullscreen, setIsFullscreen] = useState(false);
  const [showFitMenu, setShowFitMenu] = useState(false);
  const [fitLabel, setFitLabel] = useState('Fit');
  const fitMenuRef = useRef<HTMLDivElement>(null);
  const [canvasSize, setCanvasSize] = useState({ width: 0, height: 0 });

  // Compute canvas pixel size from container via ResizeObserver
  useEffect(() => {
    const el = containerRef.current;
    if (!el) return;
    const compute = () => {
      const { width, height } = el.getBoundingClientRect();
      const pad = 32; // 16px each side
      const aw = width - pad;
      const ah = height - pad;
      if (aw <= 0 || ah <= 0) return;
      const [rw, rh] = getAspectRatioDims(state.aspectRatio);
      const canvasAspect = rw / rh;
      const containerAspect = aw / ah;
      let cw: number, ch: number;
      if (containerAspect > canvasAspect) {
        // Container wider than canvas → height-constrained
        ch = ah;
        cw = ch * canvasAspect;
      } else {
        // Container taller than canvas → width-constrained
        cw = aw;
        ch = cw / canvasAspect;
      }
      setCanvasSize({ width: Math.round(cw), height: Math.round(ch) });
    };
    compute();
    const obs = new ResizeObserver(compute);
    obs.observe(el);
    return () => obs.disconnect();
  }, [state.aspectRatio]);

  // Sync video element with playing state
  useEffect(() => {
    const vid = videoRef.current;
    if (!vid) return;
    if (state.isPlaying) {
      vid.play().catch(() => {});
    } else {
      vid.pause();
    }
  }, [state.isPlaying, state.previewAssetId]);

  // Sync video currentTime with store
  useEffect(() => {
    const vid = videoRef.current;
    if (!vid || state.isPlaying || state.previewAssetId) return;
    if (Math.abs(vid.currentTime - state.currentTime) > 0.2) {
      vid.currentTime = state.currentTime;
    }
  }, [state.currentTime, state.isPlaying, state.previewAssetId]);

  // Fullscreen handling
  useEffect(() => {
    function onChange() {
      setIsFullscreen(!!document.fullscreenElement);
    }
    document.addEventListener('fullscreenchange', onChange);
    return () => document.removeEventListener('fullscreenchange', onChange);
  }, []);

  // Close fit menu on outside click
  useEffect(() => {
    function onOutside(e: MouseEvent) {
      if (fitMenuRef.current && !fitMenuRef.current.contains(e.target as Node)) {
        setShowFitMenu(false);
      }
    }
    if (showFitMenu) document.addEventListener('mousedown', onOutside);
    return () => document.removeEventListener('mousedown', onOutside);
  }, [showFitMenu]);

  const handleFullscreen = useCallback(() => {
    if (!containerRef.current) return;
    if (!document.fullscreenElement) {
      containerRef.current.requestFullscreen();
    } else {
      document.exitFullscreen();
    }
  }, []);

  const handleSkipBack = useCallback(() => {
    if (state.previewAssetId) dispatch({ type: 'SET_PREVIEW_ASSET', id: null });
    dispatch({ type: 'SET_CURRENT_TIME', time: 0 });
    dispatch({ type: 'SET_PLAYING', playing: false });
  }, [dispatch, state.previewAssetId]);

  const handleSkipForward = useCallback(() => {
    if (state.previewAssetId) dispatch({ type: 'SET_PREVIEW_ASSET', id: null });
    dispatch({ type: 'SET_CURRENT_TIME', time: state.duration });
    dispatch({ type: 'SET_PLAYING', playing: false });
  }, [dispatch, state.duration, state.previewAssetId]);

  // Find a video asset currently at playhead
  const isPreviewingLibrary = state.previewAssetId !== null;
  const activeItem = state.items.find(item =>
    item.startTime <= state.currentTime &&
    item.startTime + item.duration >= state.currentTime
  );
  
  const activeAsset = isPreviewingLibrary
    ? state.assets.find(a => a.id === state.previewAssetId)
    : (activeItem ? state.assets.find(a => a.id === activeItem.assetId) : null);

  const getEffectFilter = (effect?: string): string => {
    if (!effect) return '';
    switch (effect) {
      case 'Blur': return 'blur(6px)';
      case 'Retro': return 'sepia(0.5) contrast(1.1) saturate(0.9)';
      case 'Glow': return 'brightness(1.2) saturate(1.2)';
      default: return '';
    }
  };

  const getTextStyles = (name: string) => {
    const lower = name.toLowerCase();
    if (lower.includes('neon') || lower.includes('glow')) {
      return {
        color: '#fff',
        textShadow: '0 0 5px #fff, 0 0 10px #6366f1, 0 0 20px #6366f1, 0 0 30px #6366f1',
      };
    }
    if (lower.includes('glitch')) {
      return {
        color: '#fff',
        textShadow: '1.5px -1.5px 0 #ff0055, -1.5px 1.5px 0 #00fffa',
        fontFamily: 'monospace',
      };
    }
    if (lower.includes('bold') || lower.includes('title')) {
      return {
        fontSize: '2rem',
        fontWeight: 900,
        letterSpacing: '-0.04em',
        color: '#fff',
      };
    }
    return {
      color: '#fff',
      fontWeight: 500,
      fontSize: '1.25rem',
    };
  };

  let currentOpacity = activeItem ? activeItem.opacity : 1;
  if (!isPreviewingLibrary && activeItem) {
    if (activeItem.transition === 'Fade') {
      const elapsed = state.currentTime - activeItem.startTime;
      const fadeDuration = 0.5;
      if (elapsed >= 0 && elapsed < fadeDuration) {
        currentOpacity = (elapsed / fadeDuration) * activeItem.opacity;
      }
    }
  }

  return (
    <div className="flex-1 flex flex-col bg-[#09090B]">
      {/* Video Canvas Area */}
      <div className="flex-1 flex items-center justify-center relative" ref={containerRef}>
        <div
          ref={canvasRef}
          className="bg-black rounded-lg border-2 border-indigo-500/50 shadow-2xl relative overflow-hidden flex items-center justify-center"
          style={{
            width: canvasSize.width || undefined,
            height: canvasSize.height || undefined,
          }}
        >


          {/* Vignette effect overlay */}
          {!isPreviewingLibrary && activeItem?.effect === 'Vignette' && (
            <div className="absolute inset-0 pointer-events-none shadow-[inset_0_0_80px_rgba(0,0,0,0.85)] rounded-lg z-20" />
          )}

          {/* Film Grain noise overlay */}
          {!isPreviewingLibrary && activeItem?.effect === 'Film Grain' && (
            <div
              className="absolute inset-0 pointer-events-none opacity-[0.06] z-20 bg-repeat"
              style={{
                backgroundImage: `url("data:image/svg+xml,%3Csvg viewBox='0 0 200 200' xmlns='http://www.w3.org/2000/svg'%3E%3Cfilter id='noiseFilter'%3E%3CfeTurbulence type='fractalNoise' baseFrequency='0.8' numOctaves='3' stitchTiles='stitch'/%3E%3C/filter%3E%3Crect width='100%25' height='100%25' filter='url(%23noiseFilter)'/%3E%3C/svg%3E")`
              }}
            />
          )}

          {activeAsset?.type === 'video' ? (
            <video
              ref={videoRef}
              src={activeAsset.url}
              className="w-full h-full object-contain"
              playsInline
              style={!isPreviewingLibrary && activeItem ? {
                transform: `translate(${activeItem.x}px, ${activeItem.y}px) rotate(${activeItem.rotation}deg) scale(${activeItem.scaleX}, ${activeItem.scaleY})`,
                opacity: currentOpacity,
                transformOrigin: 'center',
                transition: 'transform 0.1s ease-out, opacity 0.1s ease-out',
                filter: getEffectFilter(activeItem.effect),
              } : {}}
              onEnded={() => dispatch({ type: 'SET_PLAYING', playing: false })}
            />
          ) : activeAsset?.type === 'image' ? (
            <img
              src={activeAsset.url}
              alt=""
              className="w-full h-full object-contain"
              style={!isPreviewingLibrary && activeItem ? {
                transform: `translate(${activeItem.x}px, ${activeItem.y}px) rotate(${activeItem.rotation}deg) scale(${activeItem.scaleX}, ${activeItem.scaleY})`,
                opacity: currentOpacity,
                transformOrigin: 'center',
                transition: 'transform 0.1s ease-out, opacity 0.1s ease-out',
                filter: getEffectFilter(activeItem.effect),
              } : {}}
            />
          ) : activeAsset?.type === 'text' && activeItem ? (
            <div
              className="absolute text-center px-4 select-none drop-shadow-[0_2px_8px_rgba(0,0,0,0.8)]"
              style={{
                transform: `translate(${activeItem.x}px, ${activeItem.y}px) rotate(${activeItem.rotation}deg) scale(${activeItem.scaleX}, ${activeItem.scaleY})`,
                opacity: currentOpacity,
                transformOrigin: 'center',
                transition: 'transform 0.1s ease-out, opacity 0.1s ease-out',
                filter: getEffectFilter(activeItem.effect),
                ...getTextStyles(activeAsset.name),
              }}
            >
              {activeAsset.name}
            </div>
          ) : (
            <div className="flex flex-col items-center gap-3 opacity-30">
              <div className="w-16 h-16 rounded-xl bg-white/5 flex items-center justify-center">
                <Play className="w-8 h-8 fill-current text-zinc-400 ml-1" />
              </div>
              <p className="text-xs text-zinc-500">Add media to preview</p>
            </div>
          )}

          {/* Playhead overlay for images/empty */}
          {state.isPlaying && !activeAsset && (
            <div className="absolute inset-0 flex items-center justify-center">
              <div className="text-zinc-600 text-4xl font-mono font-light">
                {formatTime(state.currentTime)}
              </div>
            </div>
          )}
        </div>
      </div>

      {/* Playback Controls */}
      <div className="h-12 border-t border-white/5 flex items-center justify-between px-4 bg-[#0E0E11]">
        {/* Timecode */}
        <div className="text-xs text-indigo-400 font-mono flex items-center gap-2">
          <span>{formatTime(state.currentTime)}</span>
          <span className="text-zinc-600">/</span>
          <span className="text-zinc-500">{formatTime(state.duration)}</span>
        </div>

        {/* Transport controls */}
        <div className="flex items-center gap-4">
          <Button
            variant="ghost"
            size="icon"
            className="w-8 h-8 text-zinc-400 hover:text-white"
            onClick={handleSkipBack}
            title="Go to start"
          >
            <SkipBack className="w-4 h-4" />
          </Button>
          <Button
            variant="ghost"
            size="icon"
            className="w-8 h-8 text-zinc-400 hover:text-white"
            onClick={togglePlay}
            title={state.isPlaying ? 'Pause (Space)' : 'Play (Space)'}
          >
            {state.isPlaying
              ? <Pause className="w-5 h-5 fill-current" />
              : <Play className="w-5 h-5 fill-current" />
            }
          </Button>
          <Button
            variant="ghost"
            size="icon"
            className="w-8 h-8 text-zinc-400 hover:text-white"
            onClick={handleSkipForward}
            title="Go to end"
          >
            <SkipForward className="w-4 h-4" />
          </Button>
        </div>

        {/* View controls */}
        <div className="flex items-center gap-2">
          <Button
            variant="ghost"
            size="icon"
            className="w-7 h-7 text-zinc-400 hover:text-white"
            onClick={handleFullscreen}
            title={isFullscreen ? 'Exit fullscreen' : 'Fullscreen'}
          >
            {isFullscreen ? <Minimize className="w-4 h-4" /> : <Maximize className="w-4 h-4" />}
          </Button>
          <div className="relative" ref={fitMenuRef}>
            <div
              className="flex items-center gap-1 bg-white/5 px-2 py-1 rounded text-xs text-zinc-300 cursor-pointer hover:bg-white/10"
              onClick={() => setShowFitMenu(v => !v)}
            >
              <span>{fitLabel}</span>
              <ChevronDown className="w-3 h-3" />
            </div>
            {showFitMenu && (
              <div className="absolute bottom-full mb-1 right-0 bg-[#1a1a22] border border-white/10 rounded-lg shadow-xl py-1 z-50 min-w-[80px]">
                {FIT_OPTIONS.map(opt => (
                  <div
                    key={opt}
                    className={`px-3 py-1.5 text-xs cursor-pointer hover:bg-white/10 transition-colors ${opt === fitLabel ? 'text-indigo-400' : 'text-zinc-300'}`}
                    onClick={() => { setFitLabel(opt); setShowFitMenu(false); }}
                  >
                    {opt}
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
