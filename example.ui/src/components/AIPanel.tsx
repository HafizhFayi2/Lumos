import { Bot, Sparkles, Wand2, TerminalSquare, SendHorizonal, Scissors } from 'lucide-react';
import { cn } from '../lib/utils';
import { useEditor } from '../context/EditorContext';
import { useState } from 'react';

export default function AIPanel() {
  const { messages, addMessage, updateMessage } = useEditor();
  const [input, setInput] = useState('');

  const handleSend = () => {
    if (!input.trim()) return;
    
    addMessage({
      sender: 'user',
      text: input
    });
    
    const userText = input;
    setInput('');
    
    // Simulate AI response
    setTimeout(() => {
      const aiMsgId = Date.now().toString();
      addMessage({
        sender: 'assistant',
        text: 'Processing your request...',
        isGenerating: true,
        tool: 'process_video',
        status: 'running',
        logs: ['Analyzing timeline...', 'Extracting features...']
      });
      
      setTimeout(() => {
        updateMessage(aiMsgId, {
          text: `I have completed the task based on: "${userText}".`,
          isGenerating: false,
          status: 'success',
          logs: ['Analyzing timeline...', 'Extracting features...', 'Done.']
        });
      }, 2000);
    }, 500);
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) {
      handleSend();
    }
  };

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
        {messages.map(msg => (
          <ChatMessage key={msg.id} sender={msg.sender} message={msg.text} isGenerating={msg.isGenerating}>
            {msg.tool && (
              <MCPActivityBlock 
                tool={msg.tool} 
                status={msg.status || 'success'} 
                logs={msg.logs || []} 
              />
            )}
          </ChatMessage>
        ))}
      </div>

      {/* Input Area */}
      <div className="p-3 bg-surface-soft border-t border-hairline">
        {/* Suggested Actions */}
        <div className="flex gap-2 overflow-x-auto no-scrollbar mb-3 pb-1">
          <SuggestedAction icon={Scissors} label="Remove silences" />
          <SuggestedAction icon={Wand2} label="Auto color grade" />
          <SuggestedAction icon={Sparkles} label="Generate subtitles" />
        </div>
        
        <div className="relative group">
          <textarea 
            rows={2}
            value={input}
            onChange={(e) => setInput(e.target.value)}
            onKeyDown={handleKeyDown}
            placeholder="Ask AI to edit, analyze, or generate..."
            className="w-full bg-surface-card-strong border border-hairline rounded-lg pl-3 pr-10 py-3 text-[13px] text-ink placeholder:text-muted focus:outline-none focus:border-primary/50 focus:ring-2 focus:ring-primary/20 focus:bg-surface-elevated transition-all resize-none shadow-inner"
          ></textarea>
          <button 
            onClick={handleSend}
            className="absolute right-2 bottom-2 w-8 h-8 flex items-center justify-center bg-primary hover:bg-primary-active text-white rounded-md transition-all shadow-md hover:scale-105 active:scale-95"
          >
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
    <div className={cn("flex flex-col max-w-[92%]", sender === 'user' ? "self-end items-end" : "self-start items-start")}>
      <div className="flex items-center gap-1.5 mb-1 px-1">
        {sender === 'assistant' ? (
          <>
            <Bot className="w-4 h-4 text-primary" />
            <span className="text-[11px] font-semibold text-ink tracking-wide">Palmier AI</span>
          </>
        ) : (
          <span className="text-[11px] font-medium text-muted">You</span>
        )}
      </div>
      <div className={cn(
        "rounded-2xl p-3.5 shadow-md text-[13px] leading-relaxed relative overflow-hidden transition-all duration-300",
        sender === 'user' 
          ? "bg-primary/20 border border-primary/30 text-ink rounded-tr-sm backdrop-blur-sm" 
          : "bg-surface-card-strong border border-hairline text-ink w-full rounded-tl-sm shadow-[0_4px_15px_rgba(0,0,0,0.2)]"
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
