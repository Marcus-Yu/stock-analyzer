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

// Map metric labels to LLM assessment keys
const METRIC_ASSESSMENT_KEYS: Record<string, string> = {
  'Price-to-Earnings': 'pe_ratio',
  'Price-to-Book': 'pb_ratio',
  'Price-to-Sales (TTM)': 'ps_ttm',
  'EV / EBITDA': 'ev_ebitda',
  'Gross Margin': 'gross_margin',
  'Revenue Growth (YoY)': 'revenue_growth',
  'Debt-to-Equity': 'debt_equity',
  'Current Ratio': 'current_ratio',
  'Return on Equity': 'roe',
  'Dividend Yield': 'dividend_yield',
  'Beta': 'beta',
  'Market Cap': 'market_cap',
};

function getMetricColor(
  label: string,
  value: number | null,
  assessments: Record<string, string> | undefined,
): string {
  if (value == null) return 'text-muted/40';

  // Use LLM-provided assessment if available
  const assessmentKey = METRIC_ASSESSMENT_KEYS[label];
  if (assessments && assessmentKey) {
    const assessment = assessments[assessmentKey]?.toLowerCase();
    if (assessment === 'favorable') return 'text-green-600';
    if (assessment === 'unfavorable') return 'text-red-500';
    if (assessment === 'neutral') return 'text-surface';
  }

  // Fallback: default color if LLM didn't provide an assessment
  return 'text-surface';
}

const SECTIONS: { key: string; icon: string; title: string; field: keyof StockAnalysisResult; labelField: keyof StockAnalysisResult }[] = [
  { key: 'moat', icon: 'security', title: 'TECHNICAL MOAT', field: 'technical_moat', labelField: 'moat_label' },
  { key: 'catalysts', icon: 'bolt', title: 'CATALYSTS', field: 'catalysts', labelField: 'catalysts_label' },
  { key: 'asymmetry', icon: 'balance', title: 'PRICE ASYMMETRY', field: 'price_asymmetry', labelField: 'asymmetry_label' },
  { key: 'bench', icon: 'leaderboard', title: 'BENCHMARKING', field: 'financial_benchmarking', labelField: 'benchmarking_label' },
  { key: 'risk', icon: 'warning', title: 'RISK ASSESSMENT', field: 'risk_assessment', labelField: 'risk_label' },
];

export function AnalysisDetail({ analysis, onClose }: AnalysisDetailProps) {
  const isPositive = (analysis.priceChangePercent ?? 0) >= 0;
  const assessments = analysis.metric_assessments;

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

  const topSections = SECTIONS.slice(0, 3);
  const bottomSections = SECTIONS.slice(3);

  return (
    <div className="animate-fade-in-up" id={`detail-${analysis.ticker}`}>
      {/* Header Card */}
      <div className="bg-white rounded-5xl p-8 md:p-12 shadow-minimal border border-divider mb-6">
        <div className="flex items-center justify-between mb-8">
          <div className="flex items-center gap-6">
            <ScoreCircle score={analysis.rating} size={100} />
            <div>
              <h2 className="text-5xl font-black tracking-tighter">{analysis.ticker}</h2>
              <p className="text-lg font-bold text-muted">{analysis.companyName}</p>
              <div className="flex items-center gap-4 mt-2">
                {analysis.currentPrice != null && analysis.currentPrice > 0 ? (
                  <span className="text-2xl font-bold tabular-nums">
                    ${formatNumber(analysis.currentPrice)}
                  </span>
                ) : (
                  <span className="text-2xl font-bold text-muted">N/A</span>
                )}
                <span className={`text-xl font-black ${isPositive ? 'text-green-500' : 'text-red-400'}`}>
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
          <p className="text-lg font-semibold text-surface italic leading-relaxed">
            "{analysis.summary_verdict}"
          </p>
        </div>
      </div>

      {/* Top 3 Analysis Sections */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4 mb-4">
        {topSections.map(({ key, icon, title, field, labelField }, idx) => (
          <div
            key={key}
            className="bg-white rounded-4xl p-6 shadow-minimal border border-divider hover:-translate-y-0.5 transition-all duration-300 animate-fade-in-up flex flex-col"
            style={{ animationDelay: `${idx * 0.08}s` }}
          >
            <div className="flex items-center gap-2 mb-3">
              <span className="material-symbols-outlined text-spark text-lg">{icon}</span>
              <h3 className="text-base font-black text-spark uppercase tracking-widest">{title}</h3>
            </div>
            <p className="text-lg font-bold text-surface text-center my-3">
              {(analysis[labelField] as string) || '—'}
            </p>
            <p className="text-sm text-muted leading-relaxed flex-1">{analysis[field] as string}</p>
          </div>
        ))}
      </div>

      {/* Bottom 2 Analysis Sections (centered) */}
      <div className="flex justify-center gap-4 mb-6">
        {bottomSections.map(({ key, icon, title, field, labelField }, idx) => (
          <div
            key={key}
            className="bg-white rounded-4xl p-6 shadow-minimal border border-divider hover:-translate-y-0.5 transition-all duration-300 animate-fade-in-up flex flex-col w-full md:w-[calc(33.333%-0.5rem)]"
            style={{ animationDelay: `${(idx + 3) * 0.08}s` }}
          >
            <div className="flex items-center gap-2 mb-3">
              <span className="material-symbols-outlined text-spark text-lg">{icon}</span>
              <h3 className="text-base font-black text-spark uppercase tracking-widest">{title}</h3>
            </div>
            <p className="text-lg font-bold text-surface text-center my-3">
              {(analysis[labelField] as string) || '—'}
            </p>
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
