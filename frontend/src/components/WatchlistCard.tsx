import type { StockQuoteSummary } from '../types/stock';
import { ScoreCircle } from './ScoreCircle';

interface WatchlistCardProps {
  stock: StockQuoteSummary;
  onClick?: (ticker: string) => void;
  index: number;
}

export function WatchlistCard({ stock, onClick, index }: WatchlistCardProps) {
  const isPositive = stock.priceChangePercent >= 0;
  const hasScore = stock.rating != null && stock.rating > 0;

  return (
    <div
      className="min-w-[320px] h-[240px] bg-white rounded-5xl p-8 shadow-minimal border border-divider hover:-translate-y-1 transition-all duration-500 cursor-pointer animate-fade-in-up flex flex-col"
      style={{ animationDelay: `${index * 0.08}s` }}
      onClick={() => onClick?.(stock.ticker)}
      role="button"
      tabIndex={0}
      id={`watchlist-${stock.ticker}`}
    >
      {/* Top row: ticker + company name (fixed height) */}
      <div className="flex justify-between items-start mb-auto">
        <div className="min-w-0 flex-1 mr-4">
          <h3 className="text-3xl font-black mb-1">{stock.ticker}</h3>
          <p className="text-sm font-bold text-muted uppercase tracking-widest opacity-50 truncate">
            {stock.companyName || stock.ticker}
          </p>
        </div>
        <span className={`text-2xl font-black flex-shrink-0 ${isPositive ? 'text-green-500' : 'text-red-400'}`}>
          {isPositive ? '+' : ''}{stock.priceChangePercent.toFixed(1)}%
        </span>
      </div>

      {/* Bottom row: price + score (always at the bottom) */}
      <div className="flex items-center justify-between mt-auto">
        <div className="flex flex-col">
          <span className="text-4xl font-bold tabular-nums">
            ${stock.currentPrice.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
          </span>
          <span className="text-xs font-bold text-muted mt-1">Current Price</span>
        </div>
        {hasScore && (
          <>
            <div className="h-16 w-[2px] bg-divider" />
            <div className="flex flex-col items-center">
              <ScoreCircle score={stock.rating!} size={64} />
              <span className="text-[10px] font-black text-spark uppercase tracking-widest mt-2">Score</span>
            </div>
          </>
        )}
      </div>
    </div>
  );
}
