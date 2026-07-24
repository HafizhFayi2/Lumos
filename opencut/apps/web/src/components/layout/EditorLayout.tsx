import React from 'react';
import { TopBar } from './TopBar';
import { LeftSidebar } from './LeftSidebar';
import { AssetsPanel } from './AssetsPanel';
import { PreviewPanel } from './PreviewPanel';
import { PropertiesPanel } from './PropertiesPanel';
import { TimelinePanel } from './TimelinePanel';
import { EditorProvider } from '../../store/editor-store';

export function EditorLayout() {
  return (
    <EditorProvider>
      <div className="flex flex-col h-screen w-screen bg-[#0E0E11] text-zinc-100 overflow-hidden font-sans">
        <TopBar />
        
        <div className="flex flex-1 overflow-hidden">
          <LeftSidebar />
          <AssetsPanel />
          
          <div className="flex flex-col flex-1 overflow-hidden">
            <div className="flex flex-1 overflow-hidden">
              <PreviewPanel />
              <PropertiesPanel />
            </div>
            <TimelinePanel />
          </div>
        </div>
      </div>
    </EditorProvider>
  );
}
