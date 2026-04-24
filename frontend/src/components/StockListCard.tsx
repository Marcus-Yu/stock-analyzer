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
      className="group bg-white rounded-4xl p-8 shadow-minimal border border-divider hover:scale-[1.01] transition-transform cursor-pointer animate-fade-in-up"
      style={{ animationDelay: `${index * 0.1}s` }}
      onClick={() => onClick?.(stock.ticker)}
      role="button"
      tabIndex={0}
      id={`list-card-${stock.ticker}`}
    >
      <div className="flex items-center justify-between gap-6">
        {/* Left: Title, description, price, change */}
        <div className="flex-1 min-w-0">
          <h4 className="text-3xl font-black uppercase mb-0.5 text-spark">{stock.ticker}</h4>
          <p className="text-base font-bold text-muted mb-3 truncate">
            {stock.companyName || stock.ticker}
          </p>
          {stock.summaryVerdict && (
            <p className="text-sm font-medium text-muted/80 mb-4 line-clamp-2">
              {stock.summaryVerdict}
            </p>
          )}
          <div className="flex items-baseline gap-4">
            <span className="text-3xl font-bold text-surface tabular-nums">
              ${stock.currentPrice.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
            </span>
            <span className={`text-lg font-black ${isPositive ? 'text-green-500' : 'text-red-500'}`}>
              {isPositive ? '+' : ''}{stock.priceChangePercent.toFixed(1)}%
            </span>
          </div>
        </div>

        {/* Right: Score Circle */}
        {hasScore && (
          <div className="flex-shrink-0">
            <ScoreCircle score={stock.rating!} size={80} />
          </div>
        )}
      </div>
    </div>
  );
}
