import { Bot, Sparkles, Wand2, TerminalSquare, SendHorizonal, Scissors } from 'lucide-react';
import { cn } from '../lib/utils';

export default function AIPanel() {
  return (
    <div className="w-[320px] bg-surface-card border-l border-hairline flex flex-col shrink-0">
      {/* Tabs */}
      <div className="h-[40px] px-2 flex items-center border-b border-hairline">
        <div className="flex bg-surface-card-strong p-0.5 rounded-sm w-full border border-hairline">
          <button className="flex-1 py-1 text-[12px] font-semibold text-ink bg-surface-elevated rounded-sm shadow-sm flex items-center justify-center gap-1.5">
            <Bot className="w-3.5 h-3.5 text-primary" />
            Assistant
          </button>
          <button className="flex-1 py-1 text-[12px] font-medium text-muted hover:text-body rounded-sm flex items-center justify-center gap-1.5">
            <TerminalSquare className="w-3.5 h-3.5" />
            MCP Activity
          </button>
        </div>
      </div>

      {/* Chat Area */}
      <div className="flex-1 overflow-y-auto p-3 flex flex-col gap-4">
        <ChatMessage 
          sender="user" 
          message="Create a 15-second highlight reel from the B-Roll, focused on city skyline shots."
        />
        
        <ChatMessage sender="assistant">
          <p className="text-[13px] text-body mb-2">
            I'll scan the B-Roll footage for city skyline scenes and create a sequence for you.
          </p>
          
          <MCPActivityBlock 
            tool="analyze_video_content" 
            status="success" 
            logs={[
              "Analyzing B_Roll_City.mp4...",
              "Detected 4 skyline segments (00:05, 00:12, 00:45, 01:02)",
              "Extracting frames for preview..."
            ]}
          />

          <MCPActivityBlock 
            tool="timeline_insert_clips" 
            status="success" 
            logs={[
              "Slicing 4 clips.",
              "Assembling on V1 track...",
              "Applying default cross-dissolve transitions."
            ]}
          />
          
          <p className="text-[13px] text-body mt-2">
            I've created a new sequence with the 4 best skyline shots. It runs exactly 14.5 seconds. Do you want me to add an ambient audio track underneath?
          </p>
        </ChatMessage>

        <ChatMessage 
          sender="user" 
          message="Yes, add the Ambient_Score.wav and duck the volume when there's movement."
        />

        <ChatMessage sender="assistant" isGenerating>
          <MCPActivityBlock 
            tool="audio_ducking_apply" 
            status="running" 
            logs={[
              "Inserting Ambient_Score.wav to A1...",
              "Analyzing video motion vectors...",
              "Applying keyframes to A1 volume..."
            ]}
          />
        </ChatMessage>
      </div>

      {/* Input Area */}
      <div className="p-3 bg-surface-soft border-t border-hairline">
        {/* Suggested Actions */}
        <div className="flex gap-2 overflow-x-auto no-scrollbar mb-3 pb-1">
          <SuggestedAction icon={Scissors} label="Remove silences" />
          <SuggestedAction icon={Wand2} label="Auto color grade" />
          <SuggestedAction icon={Sparkles} label="Generate subtitles" />
        </div>
        
        <div className="relative">
          <textarea 
            rows={2}
            placeholder="Ask AI to edit, analyze, or generate..."
            className="w-full bg-surface-card-strong border border-hairline rounded-md pl-3 pr-10 py-2.5 text-[13px] text-ink placeholder:text-muted focus:outline-none focus:border-primary focus:ring-1 focus:ring-primary/30 resize-none"
          ></textarea>
          <button className="absolute right-2 bottom-2 w-7 h-7 flex items-center justify-center bg-primary hover:bg-accent-secondary text-white rounded-sm transition-colors shadow-sm">
            <SendHorizonal className="w-4 h-4" />
          </button>
        </div>
        <div className="mt-2 flex items-center justify-between">
          <span className="text-[10px] text-muted-soft flex items-center gap-1">
            <div className="w-1.5 h-1.5 bg-success rounded-full"></div>
            MCP Connected
          </span>
          <span className="text-[10px] text-muted-soft">
            <kbd className="font-sans px-1 py-0.5 bg-surface-card-strong rounded-xs border border-hairline">Ctrl</kbd> + <kbd className="font-sans px-1 py-0.5 bg-surface-card-strong rounded-xs border border-hairline">Enter</kbd>
          </span>
        </div>
      </div>
    </div>
  );
}

function ChatMessage({ sender, message, children, isGenerating }: { sender: 'user' | 'assistant', message?: string, children?: React.ReactNode, isGenerating?: boolean }) {
  return (
    <div className={cn("flex flex-col max-w-[90%]", sender === 'user' ? "self-end items-end" : "self-start items-start")}>
      <div className="flex items-center gap-1.5 mb-1 px-1">
        {sender === 'assistant' ? (
          <>
            <Bot className="w-3.5 h-3.5 text-accent-secondary" />
            <span className="text-[11px] font-semibold text-ink">Palmier AI</span>
          </>
        ) : (
          <span className="text-[11px] font-medium text-muted">You</span>
        )}
      </div>
      <div className={cn(
        "rounded-md p-2.5 shadow-sm text-[13px] leading-relaxed relative overflow-hidden",
        sender === 'user' 
          ? "bg-primary/15 border border-primary/20 text-ink" 
          : "bg-surface-card-strong border border-hairline text-ink w-full"
      )}>
        {isGenerating && sender === 'assistant' && (
          <div className="absolute inset-0 border-2 border-accent-secondary/50 rounded-md animate-pulse pointer-events-none"></div>
        )}
        {message && <p>{message}</p>}
        {children}
      </div>
    </div>
  );
}

function MCPActivityBlock({ tool, status, logs }: { tool: string, status: 'success' | 'running', logs: string[] }) {
  return (
    <div className="bg-surface-elevated border border-hairline rounded-sm overflow-hidden my-2 shadow-sm">
      <div className="bg-surface-elevated-soft px-2 py-1.5 flex items-center justify-between border-b border-hairline-soft">
        <div className="flex items-center gap-1.5">
          <TerminalSquare className="w-3.5 h-3.5 text-muted" />
          <span className="text-[11px] font-mono text-body">mcp: {tool}</span>
        </div>
        {status === 'running' ? (
          <div className="flex items-center gap-1 text-[10px] text-accent-secondary font-medium">
            <div className="w-1.5 h-1.5 bg-accent-secondary rounded-full animate-pulse"></div>
            EXECUTING
          </div>
        ) : (
          <div className="flex items-center gap-1 text-[10px] text-success font-medium">
            <div className="w-1.5 h-1.5 bg-success rounded-full"></div>
            SUCCESS
          </div>
        )}
      </div>
      <div className="p-2 bg-canvas font-mono text-[11px] leading-relaxed">
        {logs.map((log, i) => (
          <div key={i} className="text-muted-soft">
            <span className="text-body opacity-50 mr-2">{'>'}</span>
            {log}
          </div>
        ))}
        {status === 'running' && (
           <div className="text-accent-secondary animate-pulse mt-1">_</div>
        )}
      </div>
    </div>
  );
}

function SuggestedAction({ icon: Icon, label }: { icon: any, label: string }) {
  return (
    <button className="flex items-center gap-1.5 px-2.5 py-1.5 bg-surface-card-strong hover:bg-surface-elevated border border-hairline rounded-full whitespace-nowrap transition-colors group shrink-0">
      <Icon className="w-3 h-3 text-primary group-hover:text-accent-secondary" />
      <span className="text-[11px] font-medium text-body group-hover:text-ink">{label}</span>
    </button>
  );
}
