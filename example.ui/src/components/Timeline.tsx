import { MousePointer2, Scissors, Magnet, LayoutTemplate, SplitSquareHorizontal } from 'lucide-react';
import { cn, formatTimecode } from '../lib/utils';
import { useEditor, Clip as ClipType } from '../context/EditorContext';
import React, { useRef } from 'react';

export default function Timeline() {
  const { 
    clips, 
    currentTime, setCurrentTime,
    duration, 
    selectedClipId, setSelectedClipId,
    zoom, setZoom
  } = useEditor();

  const rulerRef = useRef<HTMLDivElement>(null);

  const handleSeek = (e: React.MouseEvent<HTMLDivElement>) => {
    if (!rulerRef.current) return;
    const rect = rulerRef.current.getBoundingClientRect();
    const x = e.clientX - rect.left;
    const percentage = Math.max(0, Math.min(1, x / rect.width));
    setCurrentTime(percentage * duration);
  };

  const playheadPercent = (currentTime / duration) * 100;

  return (
    <div className="h-[320px] bg-canvas flex flex-col shrink-0">
      {/* Timeline Toolbar */}
      <div className="h-10 bg-surface-card border-b border-hairline flex items-center justify-between px-3">
        <div className="flex items-center gap-1">
          <ToolButton icon={MousePointer2} active />
          <ToolButton icon={Scissors} />
          <ToolButton icon={SplitSquareHorizontal} />
          <div className="w-px h-5 bg-hairline mx-2"></div>
          <ToolButton icon={Magnet} active className="text-primary hover:text-primary-active" />
          <ToolButton icon={LayoutTemplate} />
        </div>
        <div className="flex items-center gap-4">
          <div className="flex items-center gap-2">
            <span className="text-[11px] text-muted font-medium">Zoom</span>
            <div className="w-32 h-1.5 bg-surface-card-strong rounded-full overflow-hidden relative">
              <div className="absolute top-0 bottom-0 left-0 bg-muted-soft rounded-full" style={{ width: `${zoom}%` }}></div>
              <div className="absolute top-1/2 -translate-y-1/2 w-3 h-3 bg-body rounded-full shadow-sm" style={{ left: `${zoom}%` }}></div>
            </div>
          </div>
        </div>
      </div>

      <div className="flex-1 flex overflow-hidden">
        {/* Track Headers (Left) */}
        <div className="w-[120px] bg-surface-card border-r border-hairline flex flex-col">
          {/* Spacer for ruler */}
          <div className="h-7 border-b border-hairline bg-surface-card"></div>
          
          <TrackHeader type="video" name="V2" />
          <TrackHeader type="video" name="V1" active />
          <div className="h-1 bg-surface-soft border-y border-hairline-soft my-0.5"></div>
          <TrackHeader type="audio" name="A1" active />
          <TrackHeader type="audio" name="A2" />
        </div>

        {/* Tracks Area (Right) */}
        <div className="flex-1 bg-surface-timeline flex flex-col relative overflow-hidden">
          {/* Ruler */}
          <div 
            ref={rulerRef}
            onClick={handleSeek}
            className="h-7 bg-canvas border-b border-hairline flex items-end px-2 overflow-hidden shrink-0 sticky top-0 z-20 cursor-pointer"
          >
            {/* Mock Ruler Ticks */}
            {Array.from({ length: 20 }).map((_, i) => (
              <div key={i} className="flex-1 flex flex-col justify-end relative h-full">
                <span className="absolute bottom-1 -left-3 text-[10px] text-muted-soft font-mono select-none">
                  {formatTimecode((i / 20) * duration).slice(3, 8)}
                </span>
                <div className="w-px h-2 bg-hairline absolute bottom-0 left-0"></div>
                <div className="w-px h-1 bg-hairline absolute bottom-0 left-1/4"></div>
                <div className="w-px h-1 bg-hairline absolute bottom-0 left-1/2"></div>
                <div className="w-px h-1 bg-hairline absolute bottom-0 left-3/4"></div>
              </div>
            ))}
          </div>

          {/* Playhead */}
          <div 
            className="absolute top-0 bottom-0 w-[1.5px] bg-error shadow-[0_0_10px_rgba(239,68,68,0.8)] z-30 pointer-events-none transition-all duration-100 ease-linear"
            style={{ left: `${playheadPercent}%` }}
          >
            <div className="absolute top-0 -left-2 w-4 h-4 bg-error flex items-center justify-center shadow-lg" style={{ clipPath: 'polygon(0 0, 100% 0, 100% 70%, 50% 100%, 0 70%)' }}>
              <div className="w-0.5 h-1.5 bg-canvas opacity-50 rounded-full"></div>
            </div>
          </div>

          {/* Tracks */}
          <div className="flex-1 relative">
            <Track>
              {clips.filter(c => c.track === 'V2').map(c => (
                <Clip key={c.id} data={c} selected={selectedClipId === c.id} onClick={() => setSelectedClipId(c.id)} totalDuration={duration} />
              ))}
            </Track>
            <Track>
              {clips.filter(c => c.track === 'V1').map(c => (
                <Clip key={c.id} data={c} selected={selectedClipId === c.id} onClick={() => setSelectedClipId(c.id)} totalDuration={duration} />
              ))}
            </Track>
            <div className="h-1 my-0.5"></div>
            <Track>
              {clips.filter(c => c.track === 'A1').map(c => (
                <Clip key={c.id} data={c} selected={selectedClipId === c.id} onClick={() => setSelectedClipId(c.id)} totalDuration={duration} />
              ))}
            </Track>
            <Track>
               {clips.filter(c => c.track === 'A2').map(c => (
                <Clip key={c.id} data={c} selected={selectedClipId === c.id} onClick={() => setSelectedClipId(c.id)} totalDuration={duration} />
              ))}
            </Track>
          </div>
        </div>
      </div>
    </div>
  );
}

function ToolButton({ icon: Icon, active, className }: { icon: any, active?: boolean, className?: string }) {
  return (
    <button className={cn(
      "w-7 h-7 flex items-center justify-center rounded-sm transition-colors",
      active ? "bg-surface-card-strong text-primary" : "text-body hover:text-ink hover:bg-surface-card-strong",
      className
    )}>
      <Icon className="w-4 h-4" />
    </button>
  );
}

function TrackHeader({ type, name, active }: { type: 'video' | 'audio', name: string, active?: boolean }) {
  return (
    <div className={cn(
      "h-14 border-b border-hairline-soft px-3 flex flex-col justify-center gap-1",
      active ? "bg-surface-card-strong" : "bg-surface-card"
    )}>
      <div className="flex items-center justify-between">
        <span className="text-[11px] font-bold text-ink">{name}</span>
        <div className="flex gap-1.5">
          <div className="w-2.5 h-2.5 rounded-sm border border-muted flex items-center justify-center cursor-pointer hover:border-body">
            <div className="w-1 h-1 bg-muted rounded-full"></div>
          </div>
        </div>
      </div>
      <span className="text-[9px] font-bold tracking-wider text-muted-soft uppercase">{type}</span>
    </div>
  );
}

function Track({ children }: { children: React.ReactNode }) {
  return (
    <div className="h-14 border-b border-hairline-soft relative group">
      {/* Background alternating slightly for grid feel */}
      <div className="absolute inset-0 bg-surface-card/10 pointer-events-none group-hover:bg-surface-card/30 transition-colors"></div>
      {children}
    </div>
  );
}

function Clip({ data, selected, onClick, totalDuration }: { data: ClipType, selected?: boolean, onClick: () => void, totalDuration: number }) {
  const bgClasses = {
    blue: "bg-gradient-to-b from-[#3b82f6]/60 to-[#1e3a8a]/60 border-[#60a5fa]/40 backdrop-blur-sm",
    green: "bg-gradient-to-b from-[#10b981]/60 to-[#064e3b]/60 border-[#34d399]/40 backdrop-blur-sm",
    accent: "bg-gradient-to-b from-[#a855f7]/60 to-[#4c1d95]/60 border-[#c084fc]/40 backdrop-blur-sm"
  };
  
  const leftPercent = (data.start / totalDuration) * 100;
  const widthPercent = (data.duration / totalDuration) * 100;

  return (
    <div 
      onClick={onClick}
      className={cn(
        "absolute top-[2px] bottom-[2px] rounded-md border-t border-t-white/20 border-l border-l-white/10 border-r border-r-black/30 border-b border-b-black/50 overflow-hidden flex flex-col group cursor-pointer shadow-md transition-all duration-200 hover:brightness-110",
        bgClasses[data.color],
        selected ? "ring-2 ring-primary ring-offset-1 ring-offset-canvas z-10 scale-[1.01] brightness-110" : ""
      )}
      style={{ left: `${leftPercent}%`, width: `${widthPercent}%` }}
    >
      <div className="px-2 py-0.5 bg-black/20 flex items-center justify-between shrink-0">
        <span className="text-[10px] text-ink font-medium truncate shrink">{data.name}</span>
        {data.isAI && (
          <span className="text-[8px] bg-accent-tertiary/20 text-accent-tertiary font-bold px-1 rounded-xs uppercase tracking-wider border border-accent-tertiary/30 shrink-0 ml-1">
            AI
          </span>
        )}
      </div>
      <div className="flex-1 flex items-center px-1 overflow-hidden opacity-50 group-hover:opacity-80 transition-opacity">
        {/* Mock Waveform / Content */}
        {data.color === 'green' ? (
          <svg width="100%" height="100%" preserveAspectRatio="none" className="stroke-current text-white/40">
            <path d="M0 10 L5 5 L10 15 L15 8 L20 12 L25 2 L30 18 L35 10 L40 6 L45 14" vectorEffect="non-scaling-stroke" strokeWidth="1" fill="none" />
          </svg>
        ) : (
          <div className="w-full h-full flex gap-0.5 opacity-20">
             {Array.from({length: 10}).map((_, i) => (
                <div key={i} className="h-full w-8 bg-white/20 shrink-0 border-r border-white/10"></div>
             ))}
          </div>
        )}
      </div>
      
      {/* Handles */}
      {selected && (
        <>
          <div className="absolute left-0 top-0 bottom-0 w-1.5 bg-primary cursor-ew-resize"></div>
          <div className="absolute right-0 top-0 bottom-0 w-1.5 bg-primary cursor-ew-resize"></div>
        </>
      )}
    </div>
  );
}
