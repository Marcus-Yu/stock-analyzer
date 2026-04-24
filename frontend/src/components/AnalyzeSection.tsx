import { useState, type FormEvent } from 'react';
import type { StockAnalysisResult } from '../types/stock';
import { ScoreCircle } from './ScoreCircle';

interface AnalyzeSectionProps {
  onSearch: (ticker: string) => void;
  loading: boolean;
  error: string | null;
  result: StockAnalysisResult | null;
  onViewDetail?: (ticker: string) => void;
}

function getSentimentLabel(rating: number): string {
  if (rating >= 90) return 'Prime';
  if (rating >= 70) return 'Favorable';
  if (rating >= 40) return 'Neutral';
  if (rating >= 20) return 'Cautious';
  return 'Adverse';
}

function getSentimentColor(rating: number): string {
  if (rating >= 70) return 'text-green-500';
  if (rating >= 40) return 'text-yellow-500';
  return 'text-red-400';
}

export function AnalyzeSection({ onSearch, loading, error, result, onViewDetail }: AnalyzeSectionProps) {
  const [ticker, setTicker] = useState('');

  const handleSubmit = (e: FormEvent) => {
    e.preventDefault();
    const cleaned = ticker.trim().toUpperCase();
    if (cleaned) onSearch(cleaned);
  };

  return (
    <section className="bg-spark rounded-6xl p-10 md:p-16 relative overflow-hidden text-white shadow-2xl border border-spark/20">
      {/* Decorative white shard */}
      <div className="absolute top-0 right-0 w-1/2 h-full bg-white opacity-[0.05] rotate-12 translate-x-1/3 pointer-events-none" />

      <div className="relative z-10 grid lg:grid-cols-2 gap-12 lg:gap-20 items-center">
        {/* Left: CTA */}
        <div>
          <h2 className="text-5xl md:text-7xl font-black leading-[0.9] tracking-tighter mb-6">Analyze</h2>
          <p className="text-xl md:text-2xl font-medium text-white/80 mb-8 max-w-lg leading-relaxed">
            Definitive intelligence powered by real-time market sentiment and institutional data flows.
          </p>

          <form onSubmit={handleSubmit} className="flex flex-col space-y-6 max-w-md">
            <div className="bg-white/20 backdrop-blur-md rounded-3xl p-3 flex items-center shadow-lg w-full">
              <input
                className="w-full bg-transparent border-none focus:ring-0 text-2xl font-bold uppercase px-6 placeholder:text-white/50 placeholder:normal-case text-white"
                placeholder="Ticker symbol..."
                type="text"
                value={ticker}
                onChange={(e) => setTicker(e.target.value.toUpperCase())}
                disabled={loading}
                maxLength={10}
                id="analyze-ticker-input"
              />
            </div>
            <div className="flex justify-center w-full">
              <button
                type="submit"
                disabled={loading || !ticker.trim()}
                className="bg-white text-spark px-12 py-5 rounded-3xl font-black text-xl hover:scale-[1.02] transition-all shadow-xl disabled:opacity-60 disabled:cursor-not-allowed flex items-center gap-3"
                id="analyze-submit-btn"
              >
                {loading ? (
                  <>
                    <span className="material-symbols-outlined animate-spin">progress_activity</span>
                    Analyzing...
                  </>
                ) : (
                  'Search'
                )}
              </button>
            </div>
          </form>

          {error && (
            <div className="mt-4 p-4 bg-red-500/20 rounded-2xl text-white text-sm font-medium">
              {error}
            </div>
          )}
        </div>

        {/* Right: Result Card */}
        <div className="bg-white text-surface rounded-5xl p-8 md:p-12 shadow-2xl border border-divider space-y-8" id="analyze-result-card">
          {result ? (
            <>
              <div className="flex justify-between items-center">
                <div className="px-5 py-2 bg-spark/10 rounded-full text-spark font-bold text-sm tracking-widest uppercase">
                  {result.ticker}
                </div>
                <span className="material-symbols-outlined text-4xl text-spark">auto_awesome</span>
              </div>

              <div className="space-y-6">
                <div className="flex justify-between items-baseline border-b border-divider pb-6">
                  <span className="text-2xl font-bold opacity-40">Financial</span>
                  <ScoreCircle score={result.rating} size={80} />
                </div>
                <div className="flex justify-between items-baseline border-b border-divider pb-6">
                  <span className="text-2xl font-bold opacity-40">Sentiment</span>
                  <span className={`text-3xl font-black uppercase tracking-tighter ${getSentimentColor(result.rating)}`}>
                    {getSentimentLabel(result.rating)}
                  </span>
                </div>
              </div>

              <p className="text-lg font-medium leading-relaxed text-muted">
                {result.summary_verdict}
              </p>

              <button
                onClick={() => onViewDetail?.(result.ticker)}
                className="w-full bg-spark/10 text-spark py-3 rounded-2xl font-bold text-sm tracking-widest uppercase hover:bg-spark/20 transition-colors"
              >
                View Full Analysis →
              </button>
            </>
          ) : (
            <>
              <div className="flex justify-between items-center">
                <div className="px-5 py-2 bg-spark/10 rounded-full text-spark font-bold text-sm tracking-widest uppercase">
                  Live Demo
                </div>
                <span className="material-symbols-outlined text-4xl text-spark">auto_awesome</span>
              </div>
              <div className="flex flex-col items-center py-8 text-center">
                <span className="material-symbols-outlined text-6xl text-muted/30 mb-4">query_stats</span>
                <p className="text-lg font-medium text-muted/60">
                  Enter a ticker symbol to run an AI-powered institutional analysis
                </p>
              </div>
            </>
          )}
        </div>
      </div>
    </section>
  );
}
