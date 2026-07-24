import React, { createContext, useContext, useReducer, useCallback, useRef } from 'react';

// ─── Types ────────────────────────────────────────────────────────────────────

export type AspectRatio = '16:9' | '9:16' | '1:1' | '4:3';
export type SidebarTab = 'Media' | 'Audio' | 'Text' | 'Elements' | 'Transitions' | 'Effects' | 'Captions' | 'Adjust' | 'More';
export type PropertiesTab = 'Properties' | 'Adjust' | 'Effects' | 'Transitions';

export interface Asset {
  id: string;
  name: string;
  type: 'video' | 'image' | 'audio';
  url: string;
  duration?: number; // seconds
  thumbnail?: string;
  file: File;
}

export interface TrackItem {
  id: string;
  assetId: string;
  trackIndex: number;
  startTime: number; // timeline position in seconds
  duration: number;
  trimStart: number;
  trimEnd: number;
  x: number;
  y: number;
  scaleX: number;
  scaleY: number;
  rotation: number;
  opacity: number;
}

export interface Track {
  id: string;
  type: 'video' | 'audio';
  name: string;
  visible: boolean;
  locked: boolean;
}

export interface EditorState {
  projectName: string;
  aspectRatio: AspectRatio;
  currentTime: number;
  duration: number;
  isPlaying: boolean;
  zoom: number; // pixels per second on timeline
  activeSidebarTab: SidebarTab;
  activePropertiesTab: PropertiesTab;
  selectedItemId: string | null;
  assets: Asset[];
  tracks: Track[];
  items: TrackItem[];
  undoStack: TrackItem[][];
  redoStack: TrackItem[][];
  lastSaved: Date;
  assetFilter: 'All' | 'Videos' | 'Photos' | 'Audio' | 'Graphics';
}

type EditorAction =
  | { type: 'SET_PROJECT_NAME'; name: string }
  | { type: 'SET_ASPECT_RATIO'; ratio: AspectRatio }
  | { type: 'SET_CURRENT_TIME'; time: number }
  | { type: 'SET_DURATION'; duration: number }
  | { type: 'SET_PLAYING'; playing: boolean }
  | { type: 'SET_ZOOM'; zoom: number }
  | { type: 'SET_SIDEBAR_TAB'; tab: SidebarTab }
  | { type: 'SET_PROPERTIES_TAB'; tab: PropertiesTab }
  | { type: 'SET_SELECTED_ITEM'; id: string | null }
  | { type: 'ADD_ASSET'; asset: Asset }
  | { type: 'ADD_TRACK' }
  | { type: 'TOGGLE_TRACK_VISIBILITY'; trackId: string }
  | { type: 'TOGGLE_TRACK_LOCK'; trackId: string }
  | { type: 'ADD_ITEM'; item: TrackItem }
  | { type: 'UPDATE_ITEM'; id: string; updates: Partial<TrackItem> }
  | { type: 'DELETE_SELECTED_ITEM' }
  | { type: 'SPLIT_ITEM_AT_PLAYHEAD' }
  | { type: 'UNDO' }
  | { type: 'REDO' }
  | { type: 'PUSH_UNDO' }
  | { type: 'MARK_SAVED' }
  | { type: 'SET_ASSET_FILTER'; filter: EditorState['assetFilter'] };

// ─── Initial State ────────────────────────────────────────────────────────────

const initialState: EditorState = {
  projectName: 'New Project',
  aspectRatio: '16:9',
  currentTime: 0,
  duration: 30,
  isPlaying: false,
  zoom: 60, // 60px per second
  activeSidebarTab: 'Media',
  activePropertiesTab: 'Properties',
  selectedItemId: null,
  assets: [],
  tracks: [
    { id: 'track-video-1', type: 'video', name: 'Video 1', visible: true, locked: false },
    { id: 'track-audio-1', type: 'audio', name: 'Audio 1', visible: true, locked: false },
  ],
  items: [],
  undoStack: [],
  redoStack: [],
  lastSaved: new Date(),
  assetFilter: 'All',
};

// ─── Reducer ──────────────────────────────────────────────────────────────────

function reducer(state: EditorState, action: EditorAction): EditorState {
  switch (action.type) {
    case 'SET_PROJECT_NAME':
      return { ...state, projectName: action.name };

    case 'SET_ASPECT_RATIO':
      return { ...state, aspectRatio: action.ratio };

    case 'SET_CURRENT_TIME':
      return { ...state, currentTime: Math.max(0, Math.min(action.time, state.duration)) };

    case 'SET_DURATION':
      return { ...state, duration: Math.max(action.duration, 1) };

    case 'SET_PLAYING':
      return { ...state, isPlaying: action.playing };

    case 'SET_ZOOM':
      return { ...state, zoom: Math.max(20, Math.min(300, action.zoom)) };

    case 'SET_SIDEBAR_TAB':
      return { ...state, activeSidebarTab: action.tab };

    case 'SET_PROPERTIES_TAB':
      return { ...state, activePropertiesTab: action.tab };

    case 'SET_SELECTED_ITEM':
      return { ...state, selectedItemId: action.id };

    case 'ADD_ASSET':
      return { ...state, assets: [...state.assets, action.asset] };

    case 'ADD_TRACK': {
      const videoTrackCount = state.tracks.filter(t => t.type === 'video').length;
      const newTrack: Track = {
        id: `track-video-${Date.now()}`,
        type: 'video',
        name: `Video ${videoTrackCount + 1}`,
        visible: true,
        locked: false,
      };
      return { ...state, tracks: [...state.tracks, newTrack] };
    }

    case 'TOGGLE_TRACK_VISIBILITY':
      return {
        ...state,
        tracks: state.tracks.map(t =>
          t.id === action.trackId ? { ...t, visible: !t.visible } : t
        ),
      };

    case 'TOGGLE_TRACK_LOCK':
      return {
        ...state,
        tracks: state.tracks.map(t =>
          t.id === action.trackId ? { ...t, locked: !t.locked } : t
        ),
      };

    case 'ADD_ITEM': {
      const savedItems = state.items;
      return {
        ...state,
        items: [...state.items, action.item],
        undoStack: [...state.undoStack, savedItems],
        redoStack: [],
        duration: Math.max(state.duration, action.item.startTime + action.item.duration),
      };
    }

    case 'UPDATE_ITEM':
      return {
        ...state,
        items: state.items.map(item =>
          item.id === action.id ? { ...item, ...action.updates } : item
        ),
      };

    case 'DELETE_SELECTED_ITEM': {
      if (!state.selectedItemId) return state;
      const savedItems = state.items;
      return {
        ...state,
        items: state.items.filter(i => i.id !== state.selectedItemId),
        selectedItemId: null,
        undoStack: [...state.undoStack, savedItems],
        redoStack: [],
      };
    }

    case 'SPLIT_ITEM_AT_PLAYHEAD': {
      if (!state.selectedItemId) return state;
      const item = state.items.find(i => i.id === state.selectedItemId);
      if (!item) return state;
      const t = state.currentTime;
      if (t <= item.startTime || t >= item.startTime + item.duration) return state;

      const savedItems = state.items;
      const leftDuration = t - item.startTime;
      const rightDuration = item.duration - leftDuration;

      const left: TrackItem = { ...item, duration: leftDuration };
      const right: TrackItem = {
        ...item,
        id: `${item.id}-split-${Date.now()}`,
        startTime: t,
        duration: rightDuration,
        trimStart: item.trimStart + leftDuration,
      };

      return {
        ...state,
        items: state.items.map(i => i.id === item.id ? left : i).concat(right),
        undoStack: [...state.undoStack, savedItems],
        redoStack: [],
      };
    }

    case 'PUSH_UNDO':
      return {
        ...state,
        undoStack: [...state.undoStack, state.items],
        redoStack: [],
      };

    case 'UNDO': {
      if (state.undoStack.length === 0) return state;
      const prev = state.undoStack[state.undoStack.length - 1];
      return {
        ...state,
        items: prev,
        undoStack: state.undoStack.slice(0, -1),
        redoStack: [state.items, ...state.redoStack],
      };
    }

    case 'REDO': {
      if (state.redoStack.length === 0) return state;
      const next = state.redoStack[0];
      return {
        ...state,
        items: next,
        undoStack: [...state.undoStack, state.items],
        redoStack: state.redoStack.slice(1),
      };
    }

    case 'MARK_SAVED':
      return { ...state, lastSaved: new Date() };

    case 'SET_ASSET_FILTER':
      return { ...state, assetFilter: action.filter };

    default:
      return state;
  }
}

// ─── Context ──────────────────────────────────────────────────────────────────

interface EditorContextValue {
  state: EditorState;
  dispatch: React.Dispatch<EditorAction>;
  // Convenience actions
  setCurrentTime: (t: number) => void;
  togglePlay: () => void;
  addAssetFromFile: (file: File) => void;
  addItemFromAsset: (assetId: string, trackId: string) => void;
  rafRef: React.MutableRefObject<number | null>;
}

const EditorContext = createContext<EditorContextValue | null>(null);

export function EditorProvider({ children }: { children: React.ReactNode }) {
  const [state, dispatch] = useReducer(reducer, initialState);
  const rafRef = useRef<number | null>(null);
  const lastFrameTime = useRef<number>(0);
  const stateRef = useRef(state);
  stateRef.current = state;

  const setCurrentTime = useCallback((t: number) => {
    dispatch({ type: 'SET_CURRENT_TIME', time: t });
  }, []);

  const animate = useCallback((timestamp: number) => {
    const elapsed = lastFrameTime.current ? (timestamp - lastFrameTime.current) / 1000 : 0;
    lastFrameTime.current = timestamp;
    const s = stateRef.current;
    const next = s.currentTime + elapsed;
    if (next >= s.duration) {
      dispatch({ type: 'SET_CURRENT_TIME', time: s.duration });
      dispatch({ type: 'SET_PLAYING', playing: false });
      rafRef.current = null;
      return;
    }
    dispatch({ type: 'SET_CURRENT_TIME', time: next });
    rafRef.current = requestAnimationFrame(animate);
  }, []);

  const togglePlay = useCallback(() => {
    const s = stateRef.current;
    if (s.isPlaying) {
      if (rafRef.current) { cancelAnimationFrame(rafRef.current); rafRef.current = null; }
      dispatch({ type: 'SET_PLAYING', playing: false });
    } else {
      if (s.currentTime >= s.duration) {
        dispatch({ type: 'SET_CURRENT_TIME', time: 0 });
      }
      dispatch({ type: 'SET_PLAYING', playing: true });
      lastFrameTime.current = 0;
      rafRef.current = requestAnimationFrame(animate);
    }
  }, [animate]);

  const addAssetFromFile = useCallback((file: File) => {
    const url = URL.createObjectURL(file);
    const ext = file.name.split('.').pop()?.toLowerCase() ?? '';
    const type: Asset['type'] =
      ['mp4', 'mov', 'webm', 'avi', 'mkv'].includes(ext) ? 'video' :
      ['mp3', 'wav', 'ogg', 'aac', 'm4a'].includes(ext) ? 'audio' : 'image';

    const asset: Asset = {
      id: `asset-${Date.now()}-${Math.random().toString(36).slice(2)}`,
      name: file.name,
      type,
      url,
      file,
    };

    if (type === 'video') {
      const video = document.createElement('video');
      video.src = url;
      video.onloadedmetadata = () => {
        dispatch({ type: 'ADD_ASSET', asset: { ...asset, duration: video.duration } });
      };
    } else if (type === 'image') {
      dispatch({ type: 'ADD_ASSET', asset: { ...asset, duration: 5 } });
    } else {
      const audio = document.createElement('audio');
      audio.src = url;
      audio.onloadedmetadata = () => {
        dispatch({ type: 'ADD_ASSET', asset: { ...asset, duration: audio.duration } });
      };
    }
  }, []);

  const addItemFromAsset = useCallback((assetId: string, trackId: string) => {
    const s = stateRef.current;
    const asset = s.assets.find(a => a.id === assetId);
    if (!asset) return;
    const trackIndex = s.tracks.findIndex(t => t.id === trackId);
    const item: TrackItem = {
      id: `item-${Date.now()}`,
      assetId,
      trackIndex,
      startTime: s.currentTime,
      duration: asset.duration ?? 5,
      trimStart: 0,
      trimEnd: 0,
      x: 0,
      y: 0,
      scaleX: 1,
      scaleY: 1,
      rotation: 0,
      opacity: 1,
    };
    dispatch({ type: 'ADD_ITEM', item });
  }, []);

  return (
    <EditorContext.Provider value={{ state, dispatch, setCurrentTime, togglePlay, addAssetFromFile, addItemFromAsset, rafRef }}>
      {children}
    </EditorContext.Provider>
  );
}

export function useEditor() {
  const ctx = useContext(EditorContext);
  if (!ctx) throw new Error('useEditor must be used inside EditorProvider');
  return ctx;
}

// ─── Utilities ────────────────────────────────────────────────────────────────

export function formatTime(seconds: number): string {
  const h = Math.floor(seconds / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  const s = Math.floor(seconds % 60);
  const f = Math.floor((seconds % 1) * 30); // 30fps
  return `${pad(h)}:${pad(m)}:${pad(s)}:${pad(f)}`;
}

function pad(n: number): string {
  return n.toString().padStart(2, '0');
}

export function assetTypeToTrackType(type: Asset['type']): 'video' | 'audio' {
  return type === 'audio' ? 'audio' : 'video';
}
