interface ScoreCircleProps {
  score: number;
  size?: number;
  white?: boolean;
}

export function ScoreCircle({ score, size = 64, white = false }: ScoreCircleProps) {
  const radius = 45;
  const circumference = 2 * Math.PI * radius;
  const clamped = Math.max(0, Math.min(100, score));
  const offset = circumference - (clamped / 100) * circumference;

  return (
    <div className="score-circle" style={{ width: size, height: size }}>
      <svg className="w-full h-full" viewBox="0 0 100 100">
        <circle
          className={white ? '' : 'bg'}
          cx="50" cy="50" r={radius}
          style={white ? { fill: 'none', strokeWidth: 6, stroke: 'rgba(255,255,255,0.2)' } : undefined}
        />
        <circle
          className={white ? '' : 'fg'}
          cx="50" cy="50" r={radius}
          strokeDasharray={circumference}
          strokeDashoffset={offset}
          style={white ? {
            fill: 'none',
            strokeWidth: 6,
            strokeLinecap: 'round',
            stroke: '#ffffff',
            transition: 'stroke-dashoffset 0.8s cubic-bezier(0.4, 0, 0.2, 1)',
          } : undefined}
        />
      </svg>
      <span className={`absolute inset-0 flex items-center justify-center font-black ${
        white ? 'text-white' : 'text-spark'
      }`} style={{ fontSize: size * 0.28 }}>
        {clamped}
      </span>
    </div>
  );
}
