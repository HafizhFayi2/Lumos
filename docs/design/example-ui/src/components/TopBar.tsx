import { Minus, Square, X, Video, Undo2, Redo2, Settings } from 'lucide-react';
import { cn } from '../lib/utils';

interface TopBarProps {
  onExport: () => void;
}

export default function TopBar({ onExport }: TopBarProps) {
  return (
    <div className="h-11 bg-canvas border-b border-hairline flex items-center justify-between select-none px-2 shrink-0">
      {/* Left Menu & Logo */}
      <div className="flex items-center h-full">
        <div className="flex items-center gap-2 px-2 mr-4">
          <div className="w-5 h-5 bg-primary rounded-sm flex items-center justify-center">
            <Video className="w-3 h-3 text-on-primary" />
          </div>
          <span className="text-sm font-medium text-ink tracking-tight">Lumos</span>
        </div>
        
        <div className="flex items-center gap-1">
          {['File', 'Edit', 'Timeline', 'View', 'Window', 'Help'].map((item) => (
            <button key={item} className="px-3 py-1 text-[13px] font-medium text-body hover:text-ink hover:bg-surface-card-strong rounded-sm transition-colors">
              {item}
            </button>
          ))}
        </div>
      </div>

      {/* Center - Project Name & Status */}
      <div className="absolute left-1/2 -translate-x-1/2 flex items-center gap-3">
        <span className="text-[13px] font-medium text-ink">Commercial_V3_Final</span>
        <span className="text-[11px] font-medium text-muted-soft flex items-center gap-1">
          <span className="w-1.5 h-1.5 rounded-full bg-success/80"></span>
          Auto-saved
        </span>
      </div>

      {/* Right - Actions & Windows Controls */}
      <div className="flex items-center h-full gap-2">
        <div className="flex items-center gap-1 mr-2">
          <button className="w-7 h-7 flex items-center justify-center text-muted hover:text-ink hover:bg-surface-card-strong rounded-sm">
            <Undo2 className="w-4 h-4" />
          </button>
          <button className="w-7 h-7 flex items-center justify-center text-muted-soft hover:text-ink hover:bg-surface-card-strong rounded-sm">
            <Redo2 className="w-4 h-4" />
          </button>
        </div>

        <button 
          onClick={onExport}
          className="h-7 px-4 bg-primary hover:bg-accent-secondary active:bg-primary-active text-on-primary text-[13px] font-medium rounded-sm transition-colors mr-4 shadow-sm"
        >
          Export
        </button>

        <button className="w-8 h-8 flex items-center justify-center text-muted hover:text-ink hover:bg-surface-card-strong rounded-sm mr-2">
          <Settings className="w-[15px] h-[15px]" />
        </button>

        {/* Windows Controls */}
        <div className="flex items-center h-full -mr-2">
          <button className="w-11 h-full flex items-center justify-center text-muted hover:bg-surface-card-strong transition-colors">
            <Minus className="w-4 h-4" />
          </button>
          <button className="w-11 h-full flex items-center justify-center text-muted hover:bg-surface-card-strong transition-colors">
            <Square className="w-3.5 h-3.5" />
          </button>
          <button className="w-11 h-full flex items-center justify-center text-muted hover:bg-error hover:text-white transition-colors">
            <X className="w-4 h-4" />
          </button>
        </div>
      </div>
    </div>
  );
}
