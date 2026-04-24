import type { StockAnalysisResult, StockQuoteSummary, CategorizedStocksResponse } from '../types/stock';

const API_BASE = '/api/stocks';

async function fetchJson<T>(url: string, options?: RequestInit): Promise<T> {
  const response = await fetch(url, {
    ...options,
    headers: { 'Content-Type': 'application/json', ...options?.headers },
  });
  if (!response.ok) {
    const error = await response.text();
    throw new Error(`API Error (${response.status}): ${error}`);
  }
  return response.json();
}

export const stockApi = {
  analyzeTicker: (ticker: string): Promise<StockAnalysisResult> =>
    fetchJson<StockAnalysisResult>(`${API_BASE}/analyze/${encodeURIComponent(ticker)}`),

  analyzeBatch: (tickers: string[]): Promise<StockAnalysisResult[]> =>
    fetchJson<StockAnalysisResult[]>(`${API_BASE}/batch`, {
      method: 'POST',
      body: JSON.stringify({ tickers }),
    }),

  getWatchlist: (): Promise<StockQuoteSummary[]> =>
    fetchJson<StockQuoteSummary[]>(`${API_BASE}/watchlist`),

  getMovers: (): Promise<StockQuoteSummary[]> =>
    fetchJson<StockQuoteSummary[]>(`${API_BASE}/movers`),

  getSteady: (): Promise<StockQuoteSummary[]> =>
    fetchJson<StockQuoteSummary[]>(`${API_BASE}/steady`),

  getHighlights: (): Promise<StockAnalysisResult[]> =>
    fetchJson<StockAnalysisResult[]>(`${API_BASE}/highlights`),

  getCategories: (): Promise<CategorizedStocksResponse> =>
    fetchJson<CategorizedStocksResponse>(`${API_BASE}/categories`),
};
