import { MousePointer2, Scissors, Magnet, LayoutTemplate, SplitSquareHorizontal } from 'lucide-react';
import { cn } from '../lib/utils';

export default function Timeline() {
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
              <div className="absolute top-0 bottom-0 left-0 w-[40%] bg-muted-soft rounded-full"></div>
              <div className="absolute top-1/2 left-[40%] -translate-y-1/2 w-3 h-3 bg-body rounded-full shadow-sm"></div>
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
          <div className="h-7 bg-canvas border-b border-hairline flex items-end px-2 overflow-hidden shrink-0 sticky top-0 z-20">
            {/* Mock Ruler Ticks */}
            {Array.from({ length: 20 }).map((_, i) => (
              <div key={i} className="flex-1 flex flex-col justify-end relative h-full">
                <span className="absolute bottom-1 -left-3 text-[10px] text-muted-soft font-mono select-none">
                  00:00:{i * 5 < 10 ? `0${i*5}` : i*5}:00
                </span>
                <div className="w-px h-2 bg-hairline absolute bottom-0 left-0"></div>
                <div className="w-px h-1 bg-hairline absolute bottom-0 left-1/4"></div>
                <div className="w-px h-1 bg-hairline absolute bottom-0 left-1/2"></div>
                <div className="w-px h-1 bg-hairline absolute bottom-0 left-3/4"></div>
              </div>
            ))}
          </div>

          {/* Playhead */}
          <div className="absolute top-0 bottom-0 left-[35%] w-px bg-error z-30 pointer-events-none">
            <div className="absolute top-0 -left-1.5 w-3 h-3.5 bg-error flex items-center justify-center shadow-md" style={{ clipPath: 'polygon(0 0, 100% 0, 100% 70%, 50% 100%, 0 70%)' }}>
              <div className="w-0.5 h-1.5 bg-canvas opacity-50 rounded-full"></div>
            </div>
          </div>

          {/* Tracks */}
          <div className="flex-1 relative">
            <Track>
              {/* V2 - Empty */}
            </Track>
            <Track>
              {/* V1 - Video Clips */}
              <Clip left="5%" width="25%" name="A001_C034.mp4" color="blue" />
              <Clip left="30.5%" width="15%" name="B_Roll_City.mp4" color="blue" selected />
              <Clip left="46%" width="20%" name="Generated_B_Roll" color="accent" isAI />
            </Track>
            <div className="h-1 my-0.5"></div>
            <Track>
              {/* A1 - Audio */}
              <Clip left="5%" width="40.5%" name="Ambient_Score.wav" color="green" />
            </Track>
            <Track>
              {/* A2 - Audio */}
              <Clip left="30.5%" width="35.5%" name="VO_Generated" color="green" isAI />
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

function Clip({ left, width, name, color, selected, isAI }: { left: string, width: string, name: string, color: 'blue' | 'green' | 'accent', selected?: boolean, isAI?: boolean }) {
  const bgClasses = {
    blue: "bg-[#2d4a6e] border-[#60cdff]/30",
    green: "bg-[#2d6e4a]/20 border-[#2d6e4a]/40",
    accent: "bg-[#4a2d6e] border-[#a060ff]/30" // Tertiary blue hue
  };

  return (
    <div 
      className={cn(
        "absolute top-1 bottom-1 rounded-sm border-2 overflow-hidden flex flex-col group cursor-pointer shadow-sm",
        bgClasses[color],
        selected ? "border-primary ring-2 ring-primary/20 z-10" : "hover:border-body"
      )}
      style={{ left, width }}
    >
      <div className="px-2 py-0.5 bg-black/20 flex items-center justify-between shrink-0">
        <span className="text-[10px] text-ink font-medium truncate shrink">{name}</span>
        {isAI && (
          <span className="text-[8px] bg-accent-tertiary/20 text-accent-tertiary font-bold px-1 rounded-xs uppercase tracking-wider border border-accent-tertiary/30 shrink-0 ml-1">
            AI
          </span>
        )}
      </div>
      <div className="flex-1 flex items-center px-1 overflow-hidden opacity-50 group-hover:opacity-80 transition-opacity">
        {/* Mock Waveform / Content */}
        {color === 'green' ? (
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
