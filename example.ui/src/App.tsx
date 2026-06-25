import { useState } from 'react';
import TopBar from './components/TopBar';
import LeftPanel from './components/LeftPanel';
import PreviewMonitor from './components/PreviewMonitor';
import Timeline from './components/Timeline';
import AIPanel from './components/AIPanel';
import ExportModal from './components/ExportModal';
import { EditorProvider } from './context/EditorContext';

export default function App() {
  const [isExporting, setIsExporting] = useState(false);

  return (
    <EditorProvider>
      <div className="h-screen w-screen flex flex-col bg-canvas text-ink font-sans overflow-hidden select-none">
        <TopBar onExport={() => setIsExporting(true)} />
        
        <div className="flex-1 flex flex-col overflow-hidden">
          <div className="flex-1 flex overflow-hidden border-b border-hairline">
            <LeftPanel />
            
            <div className="flex-1 flex flex-col min-w-0">
              <PreviewMonitor />
            </div>
            
            <AIPanel />
          </div>
          
          <Timeline />
        </div>

        {isExporting && <ExportModal onClose={() => setIsExporting(false)} />}
      </div>
    </EditorProvider>
  );
}

