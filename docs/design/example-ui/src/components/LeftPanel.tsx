import { FolderOpen, HardDrive, Image as ImageIcon, Music, Search, Video } from 'lucide-react';

export default function LeftPanel() {
  const mediaItems = [
    { id: 1, type: 'video', name: 'A001_C034.mp4', dur: '00:12:04' },
    { id: 2, type: 'video', name: 'A001_C035.mp4', dur: '00:08:15' },
    { id: 3, type: 'video', name: 'B_Roll_City.mp4', dur: '01:02:00' },
    { id: 4, type: 'audio', name: 'Ambient_Score.wav', dur: '03:45:00' },
    { id: 5, type: 'audio', name: 'VO_Take_1.wav', dur: '00:45:12' },
    { id: 6, type: 'image', name: 'Logo_Overlay.png', dur: 'Image' },
  ];

  return (
    <div className="w-[280px] bg-surface-card border-r border-hairline flex flex-col shrink-0">
      {/* Panel Header */}
      <div className="h-[40px] px-3 flex items-center gap-4 border-b border-hairline">
        <button className="text-[13px] font-semibold text-ink border-b-2 border-primary h-full">Media</button>
        <button className="text-[13px] font-medium text-muted hover:text-body h-full">Effects</button>
        <button className="text-[13px] font-medium text-muted hover:text-body h-full">Library</button>
      </div>

      {/* Toolbar */}
      <div className="p-3 border-b border-hairline flex flex-col gap-3">
        <div className="relative">
          <Search className="w-3.5 h-3.5 absolute left-2.5 top-1/2 -translate-y-1/2 text-muted" />
          <input 
            type="text" 
            placeholder="Search media..." 
            className="w-full h-7 bg-surface-card-strong border border-hairline rounded-sm pl-8 pr-2 text-[13px] text-ink placeholder:text-muted focus:outline-none focus:border-primary focus:ring-1 focus:ring-primary/20 transition-all"
          />
        </div>
        <div className="flex gap-1">
          <button className="flex-1 h-6 flex items-center justify-center gap-1.5 bg-surface-card-strong hover:bg-surface-elevated text-body text-[12px] font-medium rounded-sm border border-hairline transition-colors">
            <FolderOpen className="w-3.5 h-3.5" />
            Import
          </button>
        </div>
      </div>

      {/* Directory Tree */}
      <div className="px-2 py-2">
        <div className="flex items-center gap-2 px-2 py-1.5 text-[12px] text-ink bg-surface-card-strong rounded-sm cursor-pointer">
          <HardDrive className="w-3.5 h-3.5 text-primary" />
          <span className="font-medium">Project Assets</span>
        </div>
      </div>

      {/* Media Grid */}
      <div className="flex-1 overflow-y-auto px-2 pb-2">
        <div className="grid grid-cols-2 gap-2">
          {mediaItems.map(item => (
            <div key={item.id} className="group flex flex-col gap-1 cursor-pointer">
              <div className="aspect-video bg-surface-card-strong border border-hairline rounded-md overflow-hidden relative group-hover:border-accent-tertiary transition-colors">
                {/* Mock Thumbnail */}
                <div className="absolute inset-0 bg-gradient-to-br from-surface-elevated to-surface-timeline flex items-center justify-center">
                  {item.type === 'video' && <Video className="w-5 h-5 text-muted-soft" />}
                  {item.type === 'audio' && <Music className="w-5 h-5 text-muted-soft" />}
                  {item.type === 'image' && <ImageIcon className="w-5 h-5 text-muted-soft" />}
                </div>
                <div className="absolute bottom-1 right-1 bg-canvas/80 backdrop-blur-sm px-1.5 py-0.5 rounded-xs text-[10px] font-mono text-ink">
                  {item.dur}
                </div>
              </div>
              <div className="text-[11px] font-medium text-body group-hover:text-ink truncate px-0.5">
                {item.name}
              </div>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
