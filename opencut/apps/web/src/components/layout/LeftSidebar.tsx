import React from 'react';
import { 
  FolderOpen, Music, Type, Square, 
  ArrowRightLeft, Wand2, Subtitles, SlidersHorizontal,
  MoreHorizontal
} from 'lucide-react';
import { useEditor } from '../../store/editor-store';
import type { SidebarTab } from '../../store/editor-store';

const navItems: { icon: React.ElementType; label: SidebarTab }[] = [
  { icon: FolderOpen, label: 'Media' },
  { icon: Music, label: 'Audio' },
  { icon: Type, label: 'Text' },
  { icon: Square, label: 'Elements' },
  { icon: ArrowRightLeft, label: 'Transitions' },
  { icon: Wand2, label: 'Effects' },
  { icon: Subtitles, label: 'Captions' },
  { icon: SlidersHorizontal, label: 'Adjust' },
  { icon: MoreHorizontal, label: 'More' },
];

export function LeftSidebar() {
  const { state, dispatch } = useEditor();

  return (
    <div className="w-[72px] border-r border-white/5 flex flex-col items-center py-4 gap-2 shrink-0 bg-[#0E0E11] overflow-y-auto">
      {navItems.map((item) => {
        const Icon = item.icon;
        const active = state.activeSidebarTab === item.label;
        return (
          <div
            key={item.label}
            onClick={() => dispatch({ type: 'SET_SIDEBAR_TAB', tab: item.label })}
            className={`flex flex-col items-center justify-center w-[60px] h-[64px] rounded-xl cursor-pointer transition-colors ${
              active
                ? 'bg-indigo-500/20 text-indigo-400 border border-indigo-500/30'
                : 'text-zinc-400 hover:bg-white/5 hover:text-zinc-200'
            }`}
            title={item.label}
          >
            <Icon className="w-5 h-5 mb-1" />
            <span className="text-[10px] font-medium">{item.label}</span>
          </div>
        );
      })}
    </div>
  );
}
