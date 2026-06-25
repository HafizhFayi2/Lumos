export function cn(...classes: (string | undefined | null | false)[]) {
  return classes.filter(Boolean).join(' ');
}

export function formatTimecode(seconds: number): string {
  const h = Math.floor(seconds / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  const s = Math.floor(seconds % 60);
  const f = Math.floor((seconds % 1) * 24); // 24 fps
  
  const pad = (n: number) => n.toString().padStart(2, '0');
  
  return `${pad(h)}:${pad(m)}:${pad(s)}:${pad(f)}`;
}
