import type { StockAnalysisResult } from '../types/stock';
import { ScoreCircle } from './ScoreCircle';

interface AnalysisDetailProps {
  analysis: StockAnalysisResult;
  onClose: () => void;
}

function formatNumber(value: number): string {
  return value.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

function formatMetricCap(value: number | null): string {
  if (value == null) return '—';
  if (Math.abs(value) >= 1e12) return `${formatNumber(value / 1e12)}T`;
  if (Math.abs(value) >= 1e9) return `${formatNumber(value / 1e9)}B`;
  if (Math.abs(value) >= 1e6) return `${formatNumber(value / 1e6)}M`;
  return formatNumber(value);
}

type SentimentTone = 'buy' | 'hold' | 'sell';

const SENTIMENT_BANDS = [
  { min: 85, label: 'Strong Buy', tone: 'buy' },
  { min: 70, label: 'Medium Buy', tone: 'buy' },
  { min: 55, label: 'Weak Buy', tone: 'buy' },
  { min: 45, label: 'Hold', tone: 'hold' },
  { min: 30, label: 'Weak Sell', tone: 'sell' },
  { min: 15, label: 'Medium Sell', tone: 'sell' },
  { min: 0, label: 'Strong Sell', tone: 'sell' },
] as const;

// Map metric labels to likely LLM assessment keys. GPT output can vary slightly, so keep aliases generous.
const METRIC_ASSESSMENT_KEYS: Record<string, string[]> = {
  'Price-to-Earnings': ['pe_ratio', 'price_to_earnings', 'price_to_earnings_ratio', 'pe'],
  'Price-to-Book': ['pb_ratio', 'price_to_book', 'price_to_book_ratio', 'pb'],
  'Price-to-Sales (TTM)': ['ps_ttm', 'ps_ratio', 'price_to_sales', 'price_to_sales_ttm', 'price_to_sales_ratio'],
  'EV / EBITDA': ['ev_ebitda', 'ev_to_ebitda', 'enterprise_value_to_ebitda'],
  'Gross Margin': ['gross_margin', 'gross_margin_ttm'],
  'Revenue Growth (YoY)': ['revenue_growth', 'revenue_growth_yoy', 'yoy_revenue_growth'],
  'Debt-to-Equity': ['debt_equity', 'debt_to_equity', 'debt_to_equity_ratio'],
  'Current Ratio': ['current_ratio'],
  'Return on Equity': ['roe', 'return_on_equity', 'return_on_equity_ratio'],
  'Dividend Yield': ['dividend_yield', 'dividend_yield_percent'],
  'Beta': ['beta'],
  'Market Cap': ['market_cap', 'market_capitalization'],
};

function getSentiment(rating: number): { label: string; tone: SentimentTone } {
  return SENTIMENT_BANDS.find((band) => rating >= band.min) ?? SENTIMENT_BANDS[SENTIMENT_BANDS.length - 1];
}

function getSentimentBadgeClasses(tone: SentimentTone): string {
  if (tone === 'buy') return 'bg-[#B9FBC0] text-[#176B35] border-[#7BE495]';
  if (tone === 'sell') return 'bg-[#FFB3BA] text-[#8F1D2C] border-[#FF7F8E]';
  return 'bg-[#FFF3A6] text-[#7A5B00] border-[#FFE166]';
}

function normalizeAssessment(value: string | undefined): 'positive' | 'negative' | 'neutral' | null {
  const normalized = value?.trim().toLowerCase();
  if (!normalized) return null;

  if (['favorable', 'positive', 'bullish', 'good', 'strong', 'attractive', 'healthy', 'superior'].includes(normalized)) {
    return 'positive';
  }
  if (['unfavorable', 'negative', 'bearish', 'bad', 'weak', 'poor', 'unattractive', 'inferior'].includes(normalized)) {
    return 'negative';
  }
  if (['neutral', 'mixed', 'average', 'fair'].includes(normalized)) {
    return 'neutral';
  }

  return null;
}

function getMetricColor(
  label: string,
  value: number | null,
  assessments: Record<string, string> | undefined,
): string {
  if (value == null) return 'text-muted/40';

  // Use LLM-provided assessment if available
  const assessmentKeys = METRIC_ASSESSMENT_KEYS[label] ?? [];
  if (assessments && assessmentKeys.length > 0) {
    const assessment = assessmentKeys
      .map((key) => normalizeAssessment(assessments[key]))
      .find((status) => status != null);

    if (assessment === 'positive') return 'text-positive';
    if (assessment === 'negative') return 'text-negative';
    if (assessment === 'neutral') return 'text-yellow-600';
  }

  // Fallback: default color if LLM didn't provide an assessment
  return 'text-text-main';
}

type SectionTone = 'opportunity' | 'risk';
type SectionLevel = 'High' | 'Medium' | 'Low';

const STOCK_SECTIONS: {
  key: string;
  icon: string;
  title: string;
  field: keyof StockAnalysisResult;
  labelField: keyof StockAnalysisResult;
  tone: SectionTone;
}[] = [
  { key: 'moat', icon: 'security', title: 'TECHNICAL MOAT', field: 'technical_moat', labelField: 'moat_label', tone: 'opportunity' },
  { key: 'catalysts', icon: 'bolt', title: 'CATALYSTS', field: 'catalysts', labelField: 'catalysts_label', tone: 'opportunity' },
  { key: 'asymmetry', icon: 'balance', title: 'PRICE ASYMMETRY', field: 'price_asymmetry', labelField: 'asymmetry_label', tone: 'opportunity' },
  { key: 'bench', icon: 'leaderboard', title: 'BENCHMARKING', field: 'financial_benchmarking', labelField: 'benchmarking_label', tone: 'opportunity' },
  { key: 'risk', icon: 'warning', title: 'RISK ASSESSMENT', field: 'risk_assessment', labelField: 'risk_label', tone: 'opportunity' },
];

const ETF_SECTIONS: typeof STOCK_SECTIONS = [
  { key: 'portfolio', icon: 'donut_large', title: 'PORTFOLIO QUALITY', field: 'technical_moat', labelField: 'moat_label', tone: 'opportunity' },
  { key: 'valuation', icon: 'query_stats', title: 'VALUATION', field: 'catalysts', labelField: 'catalysts_label', tone: 'opportunity' },
  { key: 'growth', icon: 'monitoring', title: 'GROWTH & INCOME', field: 'price_asymmetry', labelField: 'asymmetry_label', tone: 'opportunity' },
  { key: 'vehicle', icon: 'speed', title: 'VEHICLE EFFICIENCY', field: 'financial_benchmarking', labelField: 'benchmarking_label', tone: 'opportunity' },
  { key: 'risk', icon: 'warning', title: 'RISK CONTROLS', field: 'risk_assessment', labelField: 'risk_label', tone: 'opportunity' },
];

const LEVEL_KEYWORDS: Record<SectionLevel, string[]> = {
  High: [
    'high',
    'strong',
    'deep',
    'durable',
    'multiple',
    'favorable',
    'undervalued',
    'premium',
    'controlled',
    'manageable',
  ],
  Medium: ['medium', 'moderate', 'balanced', 'fair', 'fairly', 'mixed', 'evolving', 'nascent'],
  Low: ['low', 'limited', 'weak', 'narrow', 'eroding', 'downside', 'overvalued', 'minimal', 'elevated', 'critical', 'severe'],
};

function getSectionLevel(label: string | undefined): SectionLevel | null {
  if (!label) return null;

  const normalized = label.trim().toLowerCase();
  if (normalized === 'high' || normalized === 'medium' || normalized === 'low') {
    return `${normalized.charAt(0).toUpperCase()}${normalized.slice(1)}` as SectionLevel;
  }

  const words = normalized.split(/[^a-z]+/).filter(Boolean);
  for (const level of ['High', 'Medium', 'Low'] as const) {
    if (LEVEL_KEYWORDS[level].some((keyword) => words.includes(keyword))) {
      return level;
    }
  }

  return null;
}

function getSectionBadgeClasses(level: SectionLevel, tone: SectionTone): string {
  const isPositive = tone === 'opportunity' ? level === 'High' : level === 'Low';
  const isNegative = tone === 'opportunity' ? level === 'Low' : level === 'High';

  if (isNegative) return 'bg-[#FFB3BA] text-[#8F1D2C] border-[#FF7F8E]';
  if (!isPositive) return 'bg-[#FFF3A6] text-[#7A5B00] border-[#FFE166]';
  return 'bg-[#B9FBC0] text-[#176B35] border-[#7BE495]';
}

function SectionLevelBadge({ label, tone }: { label: string | undefined; tone: SectionTone }) {
  const level = getSectionLevel(label);

  if (!level) {
    return (
      <span className="inline-flex min-w-24 justify-center rounded-full border border-divider bg-cream px-5 py-2 text-sm font-black uppercase tracking-widest text-muted">
        —
      </span>
    );
  }

  return (
    <span className={`inline-flex min-w-24 justify-center rounded-full border px-5 py-2 text-sm font-black uppercase tracking-widest ${getSectionBadgeClasses(level, tone)}`}>
      {level}
    </span>
  );
}

function isEtfOrIndexAnalysis(analysis: StockAnalysisResult): boolean {
  return (analysis.analysis_type ?? '').toLowerCase().includes('etf')
    || (analysis.analysis_type ?? '').toLowerCase().includes('index');
}

function formatEstimate(value: number | null | undefined): string {
  if (value == null) return '—';
  return `$${formatNumber(value)}`;
}

function getEstimateColor(value: number | null | undefined, currentPrice: number | null): string {
  if (value == null || currentPrice == null || currentPrice <= 0) return 'text-text-main';
  if (value > currentPrice) return 'text-positive';
  if (value < currentPrice) return 'text-negative';
  return 'text-text-main';
}

export function AnalysisDetail({ analysis, onClose }: AnalysisDetailProps) {
  const isPositive = (analysis.priceChangePercent ?? 0) >= 0;
  const assessments = analysis.metric_assessments;
  const isEtfOrIndex = isEtfOrIndexAnalysis(analysis);
  const sections = isEtfOrIndex ? ETF_SECTIONS : STOCK_SECTIONS;
  const priceEstimates = analysis.price_estimates ?? [];
  const sentiment = getSentiment(analysis.rating);

  const metrics = [
    { label: 'Price-to-Earnings', value: analysis.keyMetrics.peRatio },
    { label: 'Price-to-Book', value: analysis.keyMetrics.pbRatio },
    { label: 'Price-to-Sales (TTM)', value: analysis.keyMetrics.psTtm },
    { label: 'EV / EBITDA', value: analysis.keyMetrics.evToEbitda },
    { label: 'Gross Margin', value: analysis.keyMetrics.grossMargin, suffix: '%' },
    { label: 'Revenue Growth (YoY)', value: analysis.keyMetrics.revenueGrowthYoy, suffix: '%' },
    { label: 'Debt-to-Equity', value: analysis.keyMetrics.debtToEquity },
    { label: 'Current Ratio', value: analysis.keyMetrics.currentRatio },
    { label: 'Return on Equity', value: analysis.keyMetrics.roePercent, suffix: '%' },
    { label: 'Dividend Yield', value: analysis.keyMetrics.dividendYieldPercent, suffix: '%' },
    { label: 'Beta', value: analysis.keyMetrics.beta },
    { label: 'Market Cap', value: analysis.keyMetrics.marketCap, isCap: true },
  ];

  const topSections = sections.slice(0, 3);
  const bottomSections = sections.slice(3);

  return (
    <div className="animate-fade-in-up" id={`detail-${analysis.ticker}`}>
      {/* Header Card */}
      <div className="bg-white rounded-5xl p-8 md:p-12 shadow-minimal border border-divider mb-6">
        <div className="flex items-start justify-between gap-6 mb-8">
          <div className="flex items-center gap-6">
            <ScoreCircle score={analysis.rating} size={100} />
            <div>
              <div className="flex flex-wrap items-center gap-3">
                <h2 className="text-5xl font-black tracking-tighter">{analysis.ticker}</h2>
                <span className={`inline-flex rounded-full border px-4 py-2 text-xs font-black uppercase tracking-widest ${getSentimentBadgeClasses(sentiment.tone)}`}>
                  {sentiment.label}
                </span>
              </div>
              <p className="text-lg font-bold text-muted">{analysis.companyName}</p>
              <span className="mt-2 inline-flex rounded-full bg-cream px-3 py-1 text-[10px] font-black uppercase tracking-widest text-muted">
                {isEtfOrIndex ? 'GPT-5.4 ETF / Index Analysis' : 'GPT-5.4 Equity Analysis'}
              </span>
              <div className="flex items-center gap-4 mt-2">
                {analysis.currentPrice != null && analysis.currentPrice > 0 ? (
                  <span className="text-2xl font-bold tabular-nums">
                    ${formatNumber(analysis.currentPrice)}
                  </span>
                ) : (
                  <span className="text-2xl font-bold text-muted">N/A</span>
                )}
                <span className={`text-xl font-black ${isPositive ? 'text-positive' : 'text-negative'}`}>
                  {isPositive ? '+' : ''}{(analysis.priceChangePercent ?? 0).toFixed(1)}%
                </span>
              </div>
            </div>
          </div>
          <button onClick={onClose} className="w-12 h-12 rounded-full bg-divider hover:bg-red-100 transition-colors flex items-center justify-center" id="detail-close-btn">
            <span className="material-symbols-outlined text-muted">close</span>
          </button>
        </div>

        {/* Verdict */}
        <div className="bg-cream rounded-3xl p-6 border-l-4 border-spark">
          <p className="text-lg font-semibold text-text-main italic leading-relaxed">
            "{analysis.summary_verdict}"
          </p>
        </div>
      </div>

      {priceEstimates.length > 0 && (
        <div className="bg-white rounded-5xl p-6 md:p-8 shadow-minimal border border-divider mb-6 animate-fade-in-up">
          <div className="flex flex-col gap-2 sm:flex-row sm:items-end sm:justify-between mb-5">
            <div>
              <h3 className="text-sm font-black text-text-main uppercase tracking-widest">Price Estimates</h3>
              <p className="text-xs font-semibold text-muted mt-1">Scenario ranges from the current analysis</p>
            </div>
            {analysis.currentPrice != null && analysis.currentPrice > 0 && (
              <p className="text-xs font-bold text-muted tabular-nums">
                Current: ${formatNumber(analysis.currentPrice)}
              </p>
            )}
          </div>
          <div className="overflow-x-auto">
            <table className="w-full min-w-[760px] border-separate border-spacing-0 text-left">
              <thead>
                <tr className="text-[10px] font-black uppercase tracking-widest text-muted">
                  <th className="border-b border-divider pb-3 pr-4 whitespace-nowrap">Time</th>
                  <th className="border-b border-divider px-4 text-right">Low</th>
                  <th className="border-b border-divider px-4 text-right">Moderate</th>
                  <th className="border-b border-divider px-4 text-right">High</th>
                  <th className="border-b border-divider pl-4">Core Assumptions</th>
                </tr>
              </thead>
              <tbody>
                {priceEstimates.map((estimate) => (
                  <tr key={estimate.timeframe} className="align-top">
                    <td className="border-b border-divider/70 py-4 pr-4 text-sm font-black text-text-main whitespace-nowrap">{estimate.timeframe}</td>
                    <td className={`border-b border-divider/70 px-4 py-4 text-right text-sm font-black tabular-nums ${getEstimateColor(estimate.lower_estimate, analysis.currentPrice)}`}>
                      {formatEstimate(estimate.lower_estimate)}
                    </td>
                    <td className={`border-b border-divider/70 px-4 py-4 text-right text-sm font-black tabular-nums ${getEstimateColor(estimate.moderate_estimate, analysis.currentPrice)}`}>
                      {formatEstimate(estimate.moderate_estimate)}
                    </td>
                    <td className={`border-b border-divider/70 px-4 py-4 text-right text-sm font-black tabular-nums ${getEstimateColor(estimate.higher_estimate, analysis.currentPrice)}`}>
                      {formatEstimate(estimate.higher_estimate)}
                    </td>
                    <td className="border-b border-divider/70 py-4 pl-4 text-sm leading-relaxed text-muted">{estimate.assumptions || '—'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Top 3 Analysis Sections */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4 mb-4">
        {topSections.map(({ key, icon, title, field, labelField, tone }, idx) => (
          <div
            key={key}
            className="bg-white rounded-4xl p-6 shadow-minimal border border-divider hover:-translate-y-0.5 transition-all duration-300 animate-fade-in-up flex flex-col"
            style={{ animationDelay: `${idx * 0.08}s` }}
          >
            <div className="flex items-center gap-2 mb-3">
              <span className="material-symbols-outlined text-spark text-lg">{icon}</span>
              <h3 className="text-base font-black text-spark uppercase tracking-widest">{title}</h3>
            </div>
            <div className="my-3 flex justify-center">
              <SectionLevelBadge label={analysis[labelField] as string | undefined} tone={tone} />
            </div>
            <p className="text-sm text-muted leading-relaxed flex-1">{analysis[field] as string}</p>
          </div>
        ))}
      </div>

      {/* Bottom 2 Analysis Sections (centered) */}
      <div className="flex flex-col md:flex-row justify-center gap-4 mb-6">
        {bottomSections.map(({ key, icon, title, field, labelField, tone }, idx) => (
          <div
            key={key}
            className="bg-white rounded-4xl p-6 shadow-minimal border border-divider hover:-translate-y-0.5 transition-all duration-300 animate-fade-in-up flex flex-col w-full md:w-[calc(33.333%-0.5rem)]"
            style={{ animationDelay: `${(idx + 3) * 0.08}s` }}
          >
            <div className="flex items-center gap-2 mb-3">
              <span className="material-symbols-outlined text-spark text-lg">{icon}</span>
              <h3 className="text-base font-black text-spark uppercase tracking-widest">{title}</h3>
            </div>
            <div className="my-3 flex justify-center">
              <SectionLevelBadge label={analysis[labelField] as string | undefined} tone={tone} />
            </div>
            <p className="text-sm text-muted leading-relaxed flex-1">{analysis[field] as string}</p>
          </div>
        ))}
      </div>

      {/* Key Metrics Grid (AI-assessed colors: green=favorable, red=unfavorable) */}
      <div className="bg-white rounded-5xl p-8 shadow-minimal border border-divider">
        <h3 className="text-sm font-black text-muted uppercase tracking-widest mb-2">Key Metrics</h3>
        <p className="text-xs text-muted mb-6">
          Colors reflect AI assessment vs. industry peers and competitors
        </p>
        <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-6 gap-4">
          {metrics.map(({ label, value, suffix, isCap }) => {
            const color = getMetricColor(label, value, assessments);
            return (
              <div key={label} className="bg-cream rounded-2xl p-4 text-center">
                <p className="text-[10px] font-bold text-muted uppercase tracking-widest mb-2 leading-tight">{label}</p>
                <p className={`text-lg font-black tabular-nums ${color}`}>
                  {isCap
                    ? formatMetricCap(value ?? null)
                    : value != null
                      ? `${formatNumber(value)}${suffix || ''}`
                      : '—'}
                </p>
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
}
