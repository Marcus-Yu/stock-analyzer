import { useEffect, useRef, useState } from 'react';
import { useStockAnalysis } from '../hooks/useStockAnalysis';
import { WatchlistCard } from '../components/WatchlistCard';
import { AnalyzeSection } from '../components/AnalyzeSection';
import { StockListCard } from '../components/StockListCard';
import { AnalysisDetail } from '../components/AnalysisDetail';
import type { StockAnalysisResult } from '../types/stock';

export function Dashboard() {
  const {
    loading,
    error,
    tickerResult,
    watchlist,
    movers,
    steady,
    listsLoading,
    analyzeTicker,
    loadLists,
    clearResult,
  } = useStockAnalysis();

  const [detailStock, setDetailStock] = useState<StockAnalysisResult | null>(null);
  const detailRef = useRef<HTMLDivElement>(null);
  const hasLoaded = useRef(false);

  useEffect(() => {
    if (!hasLoaded.current) {
      hasLoaded.current = true;
      loadLists();
    }
  }, [loadLists]);

  useEffect(() => {
    if (tickerResult) setDetailStock(tickerResult);
  }, [tickerResult]);

  useEffect(() => {
    if (detailStock && detailRef.current) {
      detailRef.current.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }
  }, [detailStock]);

  const handleCardClick = async (ticker: string) => {
    const result = await analyzeTicker(ticker);
    if (result) setDetailStock(result);
  };

  const handleViewDetail = (ticker: string) => {
    if (tickerResult && tickerResult.ticker === ticker) setDetailStock(tickerResult);
  };

  return (
    <>
      <div className="px-6 md:px-16 py-12 space-y-20 max-w-screen-2xl mx-auto">

        {/* Detail View */}
        {detailStock && (
          <div ref={detailRef}>
            <AnalysisDetail
              analysis={detailStock}
              onClose={() => { setDetailStock(null); clearResult(); }}
            />
          </div>
        )}

        {/* Watchlist */}
        <section>
          <div className="mb-8">
            <h2 className="text-5xl font-black text-text-main tracking-tighter mb-2">Watchlist</h2>
            <p className="text-lg text-muted font-medium">
              {watchlist.length > 0
                ? 'Top-rated stocks based on AI analysis.'
                : 'Analyze stocks to populate your watchlist.'}
            </p>
          </div>
          <div className="flex overflow-x-auto gap-8 pb-8 no-scrollbar">
            {watchlist.length > 0 ? (
              watchlist.map((stock, idx) => (
                <WatchlistCard key={stock.ticker} stock={stock} onClick={handleCardClick} index={idx} />
              ))
            ) : listsLoading ? (
              Array.from({ length: 3 }).map((_, i) => (
                <div key={i} className="min-w-[320px] bg-white rounded-5xl p-8 shadow-minimal border border-divider animate-pulse-soft">
                  <div className="h-8 bg-divider rounded-xl w-24 mb-8" />
                  <div className="h-10 bg-divider rounded-xl w-32" />
                </div>
              ))
            ) : (
              <div className="flex flex-col items-center justify-center w-full py-12 text-muted/50">
                <span className="material-symbols-outlined text-5xl mb-4">visibility_off</span>
                <p className="text-lg font-bold">No stocks in watchlist yet</p>
                <p className="text-sm">Search and analyze tickers below — high-scoring stocks appear here.</p>
              </div>
            )}
          </div>
        </section>

        {/* Analyze Hero */}
        <AnalyzeSection
          onSearch={(t) => analyzeTicker(t)}
          loading={loading}
          error={error}
          result={tickerResult}
          onViewDetail={handleViewDetail}
        />

        {/* Big Movers + Steady Picks */}
        <div className="grid lg:grid-cols-2 gap-12 lg:gap-24">
          <section>
            <h2 className="text-4xl font-black mb-10 tracking-tight">Big Movers</h2>
            <div className="space-y-8">
              {listsLoading && movers.length === 0 ? (
                <div className="flex flex-col items-center py-16 text-muted">
                  <span className="material-symbols-outlined text-5xl animate-spin mb-4">progress_activity</span>
                  <p className="text-lg font-bold">Scanning market...</p>
                  <p className="text-sm mt-1 opacity-60">Fetching real-time quotes</p>
                </div>
              ) : movers.length > 0 ? (
                movers.slice(0, 5).map((stock, idx) => (
                  <StockListCard key={stock.ticker} stock={stock} onClick={handleCardClick} index={idx} />
                ))
              ) : (
                <div className="flex flex-col items-center py-16 text-muted/50">
                  <span className="material-symbols-outlined text-5xl mb-4">trending_flat</span>
                  <p className="text-lg font-bold">No big movers today</p>
                  <p className="text-sm">Markets are calm — no stocks moved more than 1%.</p>
                </div>
              )}
            </div>
          </section>

          <section>
            <h2 className="text-4xl font-black mb-10 tracking-tight">Steady Picks</h2>
            <div className="space-y-8">
              {listsLoading && steady.length === 0 ? (
                <div className="flex flex-col items-center py-16 text-muted">
                  <span className="material-symbols-outlined text-5xl animate-spin mb-4">progress_activity</span>
                  <p className="text-lg font-bold">Scanning ETFs...</p>
                </div>
              ) : steady.length > 0 ? (
                steady.slice(0, 5).map((stock, idx) => (
                  <StockListCard key={stock.ticker} stock={stock} onClick={handleCardClick} index={idx} />
                ))
              ) : (
                <div className="flex flex-col items-center py-16 text-muted/50">
                  <span className="material-symbols-outlined text-5xl mb-4">account_balance</span>
                  <p className="text-lg font-bold">No data available</p>
                </div>
              )}
            </div>
          </section>
        </div>
      </div>

      {/* Footer */}
      <footer className="px-16 py-20 text-center opacity-30">
        <p className="text-xs font-black tracking-[0.4em] uppercase">Spark Intelligence 2025 - MYU</p>
      </footer>
    </>
  );
}
