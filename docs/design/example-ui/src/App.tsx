import { useState } from 'react';
import TopBar from './components/TopBar';
import LeftPanel from './components/LeftPanel';
import PreviewMonitor from './components/PreviewMonitor';
import Timeline from './components/Timeline';
import AIPanel from './components/AIPanel';
import ExportModal from './components/ExportModal';

export default function App() {
  const [isExporting, setIsExporting] = useState(false);

  return (
    <div className="h-screen w-screen flex flex-col bg-canvas text-ink font-sans overflow-hidden select-none">
      <TopBar onExport={() => setIsExporting(true)} />
      
      <div className="flex-1 flex overflow-hidden">
        <LeftPanel />
        
        <div className="flex-1 flex flex-col min-w-0 border-r border-hairline">
          <PreviewMonitor />
          <Timeline />
        </div>
        
        <AIPanel />
      </div>

      {isExporting && <ExportModal onClose={() => setIsExporting(false)} />}
    </div>
  );
}

