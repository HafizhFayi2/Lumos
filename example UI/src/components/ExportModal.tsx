import { X, Youtube, MonitorUp, UploadCloud } from 'lucide-react';
import { cn } from '../lib/utils';

interface ExportModalProps {
  onClose: () => void;
}

export default function ExportModal({ onClose }: ExportModalProps) {
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/70 backdrop-blur-sm animate-in fade-in duration-200">
      <div className="w-full max-w-2xl bg-surface-card border border-hairline rounded-lg shadow-2xl flex flex-col overflow-hidden animate-in zoom-in-95 duration-200">
        
        {/* Header */}
        <div className="h-14 px-5 border-b border-hairline flex items-center justify-between bg-surface-elevated">
          <h2 className="text-[16px] font-semibold text-ink tracking-tight">Export Media</h2>
          <button onClick={onClose} className="w-8 h-8 flex items-center justify-center text-muted hover:text-ink hover:bg-surface-card-strong rounded-sm transition-colors">
            <X className="w-5 h-5" />
          </button>
        </div>

        <div className="flex h-[400px]">
          {/* Sidebar */}
          <div className="w-48 bg-surface-card-strong border-r border-hairline p-3 flex flex-col gap-1">
            <div className="text-[11px] font-bold text-muted-soft uppercase tracking-wider mb-2 px-2">Presets</div>
            
            <PresetButton icon={MonitorUp} label="High Quality 4K" active />
            <PresetButton icon={Youtube} label="YouTube 1080p" />
            <PresetButton icon={UploadCloud} label="Social Media (Vertical)" />
            
            <div className="h-px bg-hairline my-2 mx-2"></div>
            
            <PresetButton icon={MonitorUp} label="ProRes 422 HQ" />
            <PresetButton icon={MonitorUp} label="Audio Only (WAV)" />
          </div>

          {/* Main Form */}
          <div className="flex-1 p-6 overflow-y-auto bg-surface-card flex flex-col gap-6">
            
            <div className="flex flex-col gap-2">
              <label className="text-[12px] font-medium text-body-strong">File Name</label>
              <input 
                type="text" 
                defaultValue="Commercial_V3_Final.mp4"
                className="w-full h-9 bg-surface-card-strong border border-hairline rounded-sm px-3 text-[13px] text-ink focus:outline-none focus:border-primary focus:ring-1 focus:ring-primary/20 transition-all"
              />
            </div>

            <div className="grid grid-cols-2 gap-6">
              <div className="flex flex-col gap-4">
                <h3 className="text-[13px] font-semibold text-ink border-b border-hairline pb-2">Video Settings</h3>
                
                <SelectField label="Format" value="H.264 (MP4)" />
                <SelectField label="Resolution" value="3840 x 2160 (4K UHD)" />
                <SelectField label="Frame Rate" value="23.976 fps" />
                
                <div className="flex items-center justify-between">
                  <label className="text-[12px] font-medium text-body">Hardware Encoding</label>
                  <div className="w-8 h-4 bg-primary rounded-full relative cursor-pointer">
                    <div className="absolute right-0.5 top-0.5 w-3 h-3 bg-white rounded-full shadow-sm"></div>
                  </div>
                </div>
              </div>

              <div className="flex flex-col gap-4">
                <h3 className="text-[13px] font-semibold text-ink border-b border-hairline pb-2">Audio Settings</h3>
                
                <SelectField label="Format" value="AAC" />
                <SelectField label="Sample Rate" value="48000 Hz" />
                <SelectField label="Bitrate" value="320 kbps" />
              </div>
            </div>
          </div>
        </div>

        {/* Footer */}
        <div className="h-16 px-6 border-t border-hairline bg-surface-elevated flex items-center justify-between shrink-0">
          <div className="flex flex-col">
            <span className="text-[12px] text-muted font-medium">Estimated File Size: <span className="text-ink">1.2 GB</span></span>
            <span className="text-[11px] text-muted-soft">Duration: 00:01:23:14</span>
          </div>
          <div className="flex items-center gap-3">
            <button onClick={onClose} className="px-4 h-8 bg-surface-card-strong border border-hairline hover:bg-surface-elevated-soft text-ink text-[13px] font-medium rounded-sm transition-colors">
              Cancel
            </button>
            <button onClick={onClose} className="px-6 h-8 bg-primary hover:bg-accent-secondary active:bg-primary-active text-white text-[13px] font-medium rounded-sm transition-colors shadow-sm flex items-center gap-2">
              Start Export
            </button>
          </div>
        </div>
        
      </div>
    </div>
  );
}

function PresetButton({ icon: Icon, label, active }: { icon: any, label: string, active?: boolean }) {
  return (
    <button className={cn(
      "flex items-center gap-2.5 px-2 py-2 rounded-sm text-[12px] font-medium transition-colors w-full text-left",
      active ? "bg-primary/10 text-primary border border-primary/20" : "text-body hover:bg-surface-card hover:text-ink border border-transparent"
    )}>
      <Icon className="w-4 h-4 shrink-0" />
      <span className="truncate">{label}</span>
    </button>
  );
}

function SelectField({ label, value }: { label: string, value: string }) {
  return (
    <div className="flex flex-col gap-1.5">
      <label className="text-[11px] font-medium text-muted">{label}</label>
      <div className="h-8 bg-surface-card-strong border border-hairline rounded-sm px-2.5 flex items-center justify-between cursor-pointer hover:border-body transition-colors">
        <span className="text-[12px] text-ink">{value}</span>
        <div className="w-0 h-0 border-l-[4px] border-l-transparent border-r-[4px] border-r-transparent border-t-[5px] border-t-muted"></div>
      </div>
    </div>
  );
}
