import { Maximize2, MonitorPlay, Pause, Play, Settings2, SkipBack, SkipForward, Volume2 } from 'lucide-react';

export default function PreviewMonitor() {
  return (
    <div className="flex-1 flex flex-col bg-canvas border-b border-hairline min-h-0">
      {/* Top Header */}
      <div className="h-8 flex items-center justify-between px-4">
        <div className="flex items-center gap-2">
          <span className="text-[12px] font-medium text-body">Sequence 1</span>
          <span className="text-[10px] text-muted-soft font-mono">1920x1080 • 23.976 fps</span>
        </div>
        <div className="flex items-center gap-2">
          <button className="text-[12px] text-muted hover:text-ink px-2 py-0.5 bg-surface-card-strong rounded-sm border border-hairline">
            Fit
          </button>
          <button className="text-[12px] text-muted hover:text-ink px-2 py-0.5 bg-surface-card-strong rounded-sm border border-hairline">
            1/2 Res
          </button>
        </div>
      </div>

      {/* Video Area */}
      <div className="flex-1 flex items-center justify-center px-4 pb-2 min-h-0">
        <div className="w-full max-w-5xl aspect-video bg-black rounded-lg shadow-2xl border border-hairline overflow-hidden relative group">
          {/* Mock Video Content */}
          <div className="absolute inset-0 bg-gradient-to-tr from-[#1a1a1a] to-[#0a0a0a] flex items-center justify-center">
            {/* Rule of thirds grid simulation (faint) */}
            <div className="absolute inset-0 grid grid-cols-3 grid-rows-3 pointer-events-none">
              <div className="border-r border-b border-white/5"></div>
              <div className="border-r border-b border-white/5"></div>
              <div className="border-b border-white/5"></div>
              <div className="border-r border-b border-white/5"></div>
              <div className="border-r border-b border-white/5"></div>
              <div className="border-b border-white/5"></div>
              <div className="border-r border-white/5"></div>
              <div className="border-r border-white/5"></div>
              <div></div>
            </div>
            
            <div className="text-muted-soft flex items-center gap-3">
              <MonitorPlay className="w-12 h-12 opacity-20" />
            </div>
            
            {/* AI Generated Overlay Mock */}
            <div className="absolute top-4 right-4 bg-accent-tertiary/15 border border-accent-tertiary/30 px-3 py-1.5 rounded-sm backdrop-blur-md flex items-center gap-2 shadow-lg">
              <div className="w-2 h-2 rounded-full bg-accent-secondary animate-pulse"></div>
              <span className="text-[11px] font-medium text-accent-tertiary tracking-wide uppercase">AI Enhancing</span>
            </div>
          </div>
        </div>
      </div>

      {/* Transport Controls */}
      <div className="h-14 bg-surface-soft border-t border-hairline-soft flex items-center justify-between px-6 shrink-0">
        <div className="w-48 text-[15px] font-mono font-medium text-accent-secondary">
          00:01:23:14
        </div>
        
        <div className="flex items-center gap-4">
          <button className="w-8 h-8 flex items-center justify-center text-body hover:text-ink hover:bg-surface-card-strong rounded-sm transition-colors">
            <SkipBack className="w-4 h-4 fill-current" />
          </button>
          <button className="w-8 h-8 flex items-center justify-center text-body hover:text-ink hover:bg-surface-card-strong rounded-sm transition-colors">
            <Pause className="w-4 h-4 fill-current" />
          </button>
          <button className="w-10 h-10 flex items-center justify-center bg-surface-card-strong text-ink hover:bg-surface-elevated hover:text-primary rounded-full transition-colors shadow-sm">
            <Play className="w-4 h-4 fill-current translate-x-[1px]" />
          </button>
          <button className="w-8 h-8 flex items-center justify-center text-body hover:text-ink hover:bg-surface-card-strong rounded-sm transition-colors">
            <SkipForward className="w-4 h-4 fill-current" />
          </button>
        </div>

        <div className="w-48 flex items-center justify-end gap-3">
          <Volume2 className="w-4 h-4 text-muted" />
          <div className="w-20 h-1.5 bg-surface-card-strong rounded-full overflow-hidden">
            <div className="w-2/3 h-full bg-primary rounded-full"></div>
          </div>
          <button className="w-7 h-7 flex items-center justify-center text-muted hover:text-ink hover:bg-surface-card-strong rounded-sm">
            <Maximize2 className="w-4 h-4" />
          </button>
        </div>
      </div>
    </div>
  );
}
