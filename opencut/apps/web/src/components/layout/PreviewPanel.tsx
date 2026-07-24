import React, { useRef, useState, useCallback, useEffect } from 'react';
import {
  Play, Pause, SkipBack, SkipForward, Maximize, Minimize,
  ChevronDown
} from 'lucide-react';
import { Button } from '../ui/button';
import { useEditor, formatTime } from '../../store/editor-store';

const FIT_OPTIONS = ['Fit', '25%', '50%', '75%', '100%'];

export function PreviewPanel() {
  const { state, dispatch, togglePlay } = useEditor();
  const canvasRef = useRef<HTMLDivElement>(null);
  const videoRef = useRef<HTMLVideoElement>(null);
  const [isFullscreen, setIsFullscreen] = useState(false);
  const [showFitMenu, setShowFitMenu] = useState(false);
  const [fitLabel, setFitLabel] = useState('Fit');
  const fitMenuRef = useRef<HTMLDivElement>(null);

  // Sync video element with playing state
  useEffect(() => {
    const vid = videoRef.current;
    if (!vid) return;
    if (state.isPlaying) {
      vid.play().catch(() => {});
    } else {
      vid.pause();
    }
  }, [state.isPlaying]);

  // Sync video currentTime with store
  useEffect(() => {
    const vid = videoRef.current;
    if (!vid || state.isPlaying) return;
    if (Math.abs(vid.currentTime - state.currentTime) > 0.2) {
      vid.currentTime = state.currentTime;
    }
  }, [state.currentTime, state.isPlaying]);

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
    if (!canvasRef.current) return;
    if (!document.fullscreenElement) {
      canvasRef.current.requestFullscreen();
    } else {
      document.exitFullscreen();
    }
  }, []);

  const handleSkipBack = useCallback(() => {
    dispatch({ type: 'SET_CURRENT_TIME', time: 0 });
    dispatch({ type: 'SET_PLAYING', playing: false });
  }, [dispatch]);

  const handleSkipForward = useCallback(() => {
    dispatch({ type: 'SET_CURRENT_TIME', time: state.duration });
    dispatch({ type: 'SET_PLAYING', playing: false });
  }, [dispatch, state.duration]);

  // Find a video asset currently at playhead
  const activeItem = state.items.find(item =>
    item.startTime <= state.currentTime &&
    item.startTime + item.duration >= state.currentTime
  );
  const activeAsset = activeItem ? state.assets.find(a => a.id === activeItem.assetId) : null;

  // Aspect ratio CSS
  const aspectClass =
    state.aspectRatio === '9:16' ? 'aspect-[9/16]' :
    state.aspectRatio === '1:1' ? 'aspect-square' :
    state.aspectRatio === '4:3' ? 'aspect-[4/3]' :
    'aspect-video';

  return (
    <div className="flex-1 flex flex-col bg-[#09090B]">
      {/* Video Canvas Area */}
      <div className="flex-1 p-4 flex items-center justify-center relative" ref={canvasRef}>
        <div
          className={`${aspectClass} max-h-full max-w-full bg-black rounded-lg border border-white/5 shadow-2xl relative overflow-hidden flex items-center justify-center`}
          style={{ width: '100%' }}
        >
          {activeAsset?.type === 'video' && activeItem ? (
            <video
              ref={videoRef}
              src={activeAsset.url}
              className="w-full h-full object-contain"
              playsInline
              style={{
                transform: `translate(${activeItem.x}px, ${activeItem.y}px) rotate(${activeItem.rotation}deg) scale(${activeItem.scaleX}, ${activeItem.scaleY})`,
                opacity: activeItem.opacity,
                transformOrigin: 'center',
                transition: 'transform 0.1s ease-out, opacity 0.1s ease-out',
              }}
              onEnded={() => dispatch({ type: 'SET_PLAYING', playing: false })}
            />
          ) : activeAsset?.type === 'image' && activeItem ? (
            <img
              src={activeAsset.url}
              alt=""
              className="w-full h-full object-contain"
              style={{
                transform: `translate(${activeItem.x}px, ${activeItem.y}px) rotate(${activeItem.rotation}deg) scale(${activeItem.scaleX}, ${activeItem.scaleY})`,
                opacity: activeItem.opacity,
                transformOrigin: 'center',
                transition: 'transform 0.1s ease-out, opacity 0.1s ease-out',
              }}
            />
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
