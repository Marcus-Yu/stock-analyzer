import { useState, useCallback } from 'react';
import { stockApi } from '../api/stockApi';
import type { StockAnalysisResult, StockQuoteSummary } from '../types/stock';

export function useStockAnalysis() {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [tickerResult, setTickerResult] = useState<StockAnalysisResult | null>(null);

  const [watchlist, setWatchlist] = useState<StockQuoteSummary[]>([]);
  const [movers, setMovers] = useState<StockQuoteSummary[]>([]);
  const [steady, setSteady] = useState<StockQuoteSummary[]>([]);
  const [listsLoading, setListsLoading] = useState(false);

  const refreshWatchlist = useCallback(async () => {
    try {
      const w = await stockApi.getWatchlist();
      setWatchlist(w);
    } catch (err) {
      console.error('Failed to refresh watchlist:', err);
    }
  }, []);

  const analyzeTicker = useCallback(async (ticker: string): Promise<StockAnalysisResult | null> => {
    setLoading(true);
    setError(null);
    setTickerResult(null);
    try {
      const result = await stockApi.analyzeTicker(ticker);
      setTickerResult(result);

      // After analysis, refresh watchlist so new high-scoring stock may appear
      refreshWatchlist();

      return result;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Analysis failed';
      setError(message);
      return null;
    } finally {
      setLoading(false);
    }
  }, [refreshWatchlist]);

  const loadLists = useCallback(async () => {
    setListsLoading(true);
    try {
      const [m, s, w] = await Promise.all([
        stockApi.getMovers(),
        stockApi.getSteady(),
        stockApi.getWatchlist(),
      ]);
      setMovers(m);
      setSteady(s);
      setWatchlist(w);
    } catch (err) {
      console.error('Failed to load market data:', err);
    } finally {
      setListsLoading(false);
    }
  }, []);

  const clearResult = useCallback(() => {
    setTickerResult(null);
    setError(null);
  }, []);

  return {
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
  };
}
