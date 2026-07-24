import React, { useState, useRef, useEffect } from 'react';
import { Settings, ChevronDown, CheckCircle2, Download } from 'lucide-react';
import { Button } from '../ui/button';
import { useEditor } from '../../store/editor-store';
import type { AspectRatio } from '../../store/editor-store';

const ASPECT_RATIOS: AspectRatio[] = ['16:9', '9:16', '1:1', '4:3'];

export function TopBar() {
  const { state, dispatch } = useEditor();
  const [editingName, setEditingName] = useState(false);
  const [nameValue, setNameValue] = useState(state.projectName);
  const [showRatioMenu, setShowRatioMenu] = useState(false);
  const [showExportFeedback, setShowExportFeedback] = useState(false);
  const nameInputRef = useRef<HTMLInputElement>(null);
  const ratioMenuRef = useRef<HTMLDivElement>(null);

  // Autosave every 2 minutes
  useEffect(() => {
    const id = setInterval(() => {
      dispatch({ type: 'MARK_SAVED' });
    }, 120_000);
    return () => clearInterval(id);
  }, [dispatch]);

  // Close ratio menu on outside click
  useEffect(() => {
    function onOutside(e: MouseEvent) {
      if (ratioMenuRef.current && !ratioMenuRef.current.contains(e.target as Node)) {
        setShowRatioMenu(false);
      }
    }
    if (showRatioMenu) document.addEventListener('mousedown', onOutside);
    return () => document.removeEventListener('mousedown', onOutside);
  }, [showRatioMenu]);

  function commitName() {
    const trimmed = nameValue.trim() || 'New Project';
    dispatch({ type: 'SET_PROJECT_NAME', name: trimmed });
    setNameValue(trimmed);
    setEditingName(false);
  }

  function handleExport() {
    setShowExportFeedback(true);
    setTimeout(() => setShowExportFeedback(false), 3000);
  }

  const minutesAgo = Math.floor((Date.now() - state.lastSaved.getTime()) / 60_000);
  const savedLabel = minutesAgo < 1 ? 'Just saved' : `Saved ${minutesAgo} min ago`;

  return (
    <div className="h-14 border-b border-white/5 flex items-center justify-between px-4 shrink-0 bg-[#0E0E11] relative">
      <div className="flex items-center gap-4">
        {/* Logo */}
        <div className="w-8 h-8 bg-indigo-500 rounded-md flex items-center justify-center font-bold text-white select-none">
          O
        </div>

        {/* Editable project name */}
        {editingName ? (
          <input
            ref={nameInputRef}
            className="font-semibold text-sm bg-white/10 border border-indigo-500/50 rounded px-2 py-0.5 text-white outline-none w-44"
            value={nameValue}
            onChange={e => setNameValue(e.target.value)}
            onBlur={commitName}
            onKeyDown={e => {
              if (e.key === 'Enter') commitName();
              if (e.key === 'Escape') { setNameValue(state.projectName); setEditingName(false); }
            }}
            autoFocus
          />
        ) : (
          <div
            className="flex items-center gap-1 cursor-pointer hover:bg-white/5 px-2 py-1 rounded group"
            onClick={() => { setEditingName(true); setNameValue(state.projectName); }}
            title="Click to rename project"
          >
            <span className="font-semibold text-sm group-hover:text-white">{state.projectName}</span>
            <ChevronDown className="w-4 h-4 text-zinc-400" />
          </div>
        )}
      </div>

      <div className="flex items-center gap-4 text-xs text-zinc-400">
        {/* Aspect ratio picker */}
        <div className="relative" ref={ratioMenuRef}>
          <div
            className="flex items-center gap-1 bg-white/5 px-3 py-1.5 rounded cursor-pointer hover:bg-white/10 transition-colors"
            onClick={() => setShowRatioMenu(v => !v)}
            title="Change aspect ratio"
          >
            <span>{state.aspectRatio}</span>
            <ChevronDown className="w-3 h-3" />
          </div>
          {showRatioMenu && (
            <div className="absolute top-full mt-1 left-0 bg-[#1a1a22] border border-white/10 rounded-lg shadow-xl py-1 z-50 min-w-[100px]">
              {ASPECT_RATIOS.map(ratio => (
                <div
                  key={ratio}
                  className={`px-3 py-1.5 cursor-pointer hover:bg-white/10 transition-colors text-xs ${ratio === state.aspectRatio ? 'text-indigo-400' : 'text-zinc-300'}`}
                  onClick={() => { dispatch({ type: 'SET_ASPECT_RATIO', ratio }); setShowRatioMenu(false); }}
                >
                  {ratio}
                </div>
              ))}
            </div>
          )}
        </div>

        {/* Save status */}
        <div className="flex items-center gap-1.5">
          <span>{savedLabel}</span>
          <CheckCircle2 className="w-3.5 h-3.5 text-emerald-500" />
        </div>
      </div>

      <div className="flex items-center gap-3">
        <Button
          variant="ghost"
          className="h-8 text-xs bg-white/5 hover:bg-white/10 text-zinc-300"
          onClick={() => window.open('https://github.com/OpenCut-app/OpenCut/issues', '_blank')}
        >
          Send feedback
        </Button>
        <Button
          className="h-8 text-xs bg-indigo-500 hover:bg-indigo-600 text-white font-medium px-6 rounded-md relative"
          onClick={handleExport}
        >
          {showExportFeedback ? (
            <span className="flex items-center gap-1.5"><Download className="w-3 h-3" /> Exporting…</span>
          ) : 'Export'}
        </Button>
      </div>

      {/* Export toast */}
      {showExportFeedback && (
        <div className="absolute bottom-[-48px] right-4 bg-indigo-600 text-white text-xs px-4 py-2 rounded-lg shadow-lg z-50 flex items-center gap-2">
          <Download className="w-3.5 h-3.5" />
          Export queued — this is a demo build
        </div>
      )}
    </div>
  );
}
