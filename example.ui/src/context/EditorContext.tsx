import { createContext, useContext, useState, ReactNode, useEffect } from 'react';

export type Clip = {
  id: string;
  name: string;
  type: 'video' | 'audio';
  track: string; // 'V1', 'V2', 'A1', 'A2'
  start: number; // in seconds
  duration: number; // in seconds
  color: 'blue' | 'green' | 'accent';
  isAI?: boolean;
};

export type Message = {
  id: string;
  sender: 'user' | 'assistant';
  text: string;
  isGenerating?: boolean;
  logs?: string[];
  tool?: string;
  status?: 'running' | 'success';
};

interface EditorState {
  isPlaying: boolean;
  setIsPlaying: (val: boolean | ((prev: boolean) => boolean)) => void;
  currentTime: number;
  setCurrentTime: (val: number | ((prev: number) => number)) => void;
  duration: number;
  zoom: number;
  setZoom: (val: number) => void;
  clips: Clip[];
  setClips: (clips: Clip[]) => void;
  selectedClipId: string | null;
  setSelectedClipId: (id: string | null) => void;
  messages: Message[];
  addMessage: (msg: Omit<Message, 'id'>) => void;
  updateMessage: (id: string, updates: Partial<Message>) => void;
}

const EditorContext = createContext<EditorState | null>(null);

export function EditorProvider({ children }: { children: ReactNode }) {
  const [isPlaying, setIsPlaying] = useState(false);
  const [currentTime, setCurrentTime] = useState(83.14); // 00:01:23:14
  const [zoom, setZoom] = useState(40);
  const [selectedClipId, setSelectedClipId] = useState<string | null>('2');
  
  const [clips, setClips] = useState<Clip[]>([
    { id: '1', name: 'A001_C034.mp4', type: 'video', track: 'V1', start: 10, duration: 50, color: 'blue' },
    { id: '2', name: 'B_Roll_City.mp4', type: 'video', track: 'V1', start: 61, duration: 30, color: 'blue' },
    { id: '3', name: 'Generated_B_Roll', type: 'video', track: 'V1', start: 92, duration: 40, color: 'accent', isAI: true },
    { id: '4', name: 'Ambient_Score.wav', type: 'audio', track: 'A1', start: 10, duration: 81, color: 'green' },
    { id: '5', name: 'VO_Generated', type: 'audio', track: 'A2', start: 61, duration: 71, color: 'green', isAI: true },
  ]);

  const [messages, setMessages] = useState<Message[]>([
    { id: '1', sender: 'user', text: 'Create a 15-second highlight reel from the B-Roll, focused on city skyline shots.' },
    { id: '2', sender: 'assistant', text: "I've created a new sequence with the 4 best skyline shots. It runs exactly 14.5 seconds. Do you want me to add an ambient audio track underneath?", tool: 'timeline_insert_clips', status: 'success', logs: ["Slicing 4 clips.", "Assembling on V1 track...", "Applying default cross-dissolve transitions."] }
  ]);

  const addMessage = (msg: Omit<Message, 'id'>) => {
    setMessages(prev => [...prev, { ...msg, id: Date.now().toString() }]);
  };

  const updateMessage = (id: string, updates: Partial<Message>) => {
    setMessages(prev => prev.map(m => m.id === id ? { ...m, ...updates } : m));
  };

  // Playback logic
  useEffect(() => {
    let interval: number;
    if (isPlaying) {
      interval = window.setInterval(() => {
        setCurrentTime(t => {
          if (t >= 200) {
            setIsPlaying(false);
            return 200;
          }
          return t + 0.1;
        });
      }, 100);
    }
    return () => clearInterval(interval);
  }, [isPlaying]);

  return (
    <EditorContext.Provider value={{
      isPlaying, setIsPlaying,
      currentTime, setCurrentTime,
      duration: 200,
      zoom, setZoom,
      clips, setClips,
      selectedClipId, setSelectedClipId,
      messages, addMessage, updateMessage
    }}>
      {children}
    </EditorContext.Provider>
  );
}

export const useEditor = () => {
  const ctx = useContext(EditorContext);
  if (!ctx) throw new Error("useEditor must be used within EditorProvider");
  return ctx;
};
