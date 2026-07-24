import type { StockQuoteSummary } from '../types/stock';
import { ScoreCircle } from './ScoreCircle';

interface StockListCardProps {
  stock: StockQuoteSummary;
  onClick?: (ticker: string) => void;
  index: number;
}

export function StockListCard({ stock, onClick, index }: StockListCardProps) {
  const isPositive = stock.priceChangePercent >= 0;
  const hasScore = stock.rating != null && stock.rating > 0;

  return (
    <div
      className="bg-white rounded-5xl p-8 shadow-minimal border border-divider hover:-translate-y-1 transition-all duration-500 cursor-pointer animate-fade-in-up flex w-full"
      style={{ animationDelay: `${index * 0.1}s` }}
      onClick={() => onClick?.(stock.ticker)}
      role="button"
      tabIndex={0}
      id={`list-card-${stock.ticker}`}
    >
      {/* Left Column (2/3): Details & Pricing */}
      <div className="flex flex-col w-2/3 pr-8 border-r-2 border-divider min-h-full">
        <div className="flex justify-between items-start mb-2">
          <div className="min-w-0 flex-1 mr-4">
            <h3 className="text-3xl font-black mb-1 text-spark">{stock.ticker}</h3>
            <p className="text-sm font-bold text-muted uppercase tracking-widest opacity-50 truncate">
              {stock.companyName || stock.ticker}
            </p>
          </div>
        </div>

        {/* Description */}
        {stock.summaryVerdict && (
          <p className="text-sm font-medium text-muted/80 mb-6 mt-2 line-clamp-3">
            {stock.summaryVerdict}
          </p>
        )}

        {/* Bottom row: price + percentage */}
        <div className="mt-auto pt-2">
          <div className="flex flex-col">
            <div className="flex items-baseline gap-3">
              <span className="text-4xl font-bold tabular-nums text-text-main">
                ${stock.currentPrice.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
              </span>
              <span className={`text-xl font-black ${isPositive ? 'text-positive' : 'text-negative'}`}>
                {isPositive ? '+' : ''}{stock.priceChangePercent.toFixed(1)}%
              </span>
            </div>
            <span className="text-xs font-bold text-muted mt-1">Current Price</span>
          </div>
        </div>
      </div>

      {/* Right Column (1/3): Score Circle */}
      <div className="flex flex-col w-1/3 items-center justify-center pl-8">
        {hasScore && (
          <div className="flex flex-col items-center">
            <ScoreCircle score={stock.rating!} size={140} />
            <span className="text-sm font-black text-spark uppercase tracking-widest mt-4">Score</span>
          </div>
        )}
      </div>
    </div>
  );
}
