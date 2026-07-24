export interface StockAnalysisResult {
  ticker: string;
  companyName: string;
  currentPrice: number | null;
  priceChangePercent: number | null;
  rating: number;
  ratingLabel: string;
  analysis_type?: 'Stock' | 'ETF/Index' | string;
  technical_moat: string;
  moat_label: string;
  catalysts: string;
  catalysts_label: string;
  price_asymmetry: string;
  asymmetry_label: string;
  financial_benchmarking: string;
  benchmarking_label: string;
  risk_assessment: string;
  risk_label: string;
  summary_verdict: string;
  macro_context?: string;
  financial_metrics_review?: string;
  comparative_analysis?: string;
  final_verdict?: string;
  price_estimates?: PriceEstimate[];
  metric_assessments: Record<string, 'favorable' | 'unfavorable' | 'neutral'>;
  keyMetrics: KeyMetrics;
  analyzedAt: string;
}

export interface PriceEstimate {
  timeframe: string;
  lower_estimate: number | null;
  moderate_estimate: number | null;
  higher_estimate: number | null;
  assumptions: string;
}

export interface KeyMetrics {
  peRatio: number | null;
  pbRatio: number | null;
  psTtm: number | null;
  evToEbitda: number | null;
  grossMargin: number | null;
  revenueGrowthYoy: number | null;
  debtToEquity: number | null;
  currentRatio: number | null;
  roePercent: number | null;
  dividendYieldPercent: number | null;
  beta: number | null;
  week52High: number | null;
  week52Low: number | null;
  marketCap: number | null;
}

export interface StockQuoteSummary {
  ticker: string;
  companyName: string;
  currentPrice: number;
  priceChangePercent: number;
  rating: number | null;
  ratingLabel: string | null;
  summaryVerdict: string | null;
}

export interface CategorizedStocksResponse {
  highRisk: StockQuoteSummary[];
  lowRisk: StockQuoteSummary[];
}
