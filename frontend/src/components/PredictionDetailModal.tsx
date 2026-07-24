import { useEffect, useState } from 'react';
import type { StockAnalysisResult } from '../types/stock';

type SectionLevel = 'High' | 'Medium' | 'Low';
type SectionTone = 'opportunity' | 'risk';

const LEVEL_KEYWORDS: Record<SectionLevel, string[]> = {
  High: ['high', 'strong', 'deep', 'durable', 'multiple', 'favorable', 'undervalued', 'premium', 'controlled', 'manageable'],
  Medium: ['medium', 'moderate', 'balanced', 'fair', 'fairly', 'mixed', 'evolving', 'nascent'],
  Low: ['low', 'limited', 'weak', 'narrow', 'eroding', 'downside', 'overvalued', 'minimal', 'elevated', 'critical', 'severe'],
};

function getSectionLevel(label: string | undefined): SectionLevel | null {
  if (!label) return null;
  const normalized = label.trim().toLowerCase();
  if (normalized === 'high' || normalized === 'medium' || normalized === 'low') return `${normalized.charAt(0).toUpperCase()}${normalized.slice(1)}` as SectionLevel;
  const words = normalized.split(/[^a-z]+/).filter(Boolean);
  for (const level of ['High', 'Medium', 'Low'] as const) {
    if (LEVEL_KEYWORDS[level].some((keyword) => words.includes(keyword))) return level;
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

function SectionBadge({ label, tone }: { label: string | undefined; tone: SectionTone }) {
  const level = getSectionLevel(label);
  if (!level) {
    return <span className="inline-flex min-w-24 justify-center rounded-xl border border-divider bg-cream px-4 py-1.5 text-xs font-black uppercase tracking-widest text-muted">{label || '—'}</span>;
  }
  return <span className={`inline-flex min-w-24 justify-center rounded-xl border px-4 py-1.5 text-xs font-black uppercase tracking-widest ${getSectionBadgeClasses(level, tone)}`}>{label}</span>;
}

interface PredictionDetailModalProps {
  predictionId: string;
  onClose: () => void;
}

interface FullPredictionData {
  id: string;
  ticker: string;
  companyName: string;
  timestamp: string;
  originalAnalysisJson: string;
}

export function PredictionDetailModal({ predictionId, onClose }: PredictionDetailModalProps) {
  const [loading, setLoading] = useState(true);
  const [data, setData] = useState<FullPredictionData | null>(null);
  const [analysis, setAnalysis] = useState<StockAnalysisResult | null>(null);

  useEffect(() => {
    const fetchDetail = async () => {
      try {
        const res = await fetch(`http://localhost:5159/api/predictions/${predictionId}`);
        const json = await res.json();
        setData(json);
        if (json.originalAnalysisJson) {
          setAnalysis(JSON.parse(json.originalAnalysisJson));
        }
      } catch (err) {
        console.error('Failed to fetch prediction details:', err);
      } finally {
        setLoading(false);
      }
    };
    fetchDetail();
  }, [predictionId]);

  if (loading) {
    return (
      <div className="fixed inset-0 z-50 flex items-center justify-center bg-text-main/20 backdrop-blur-sm p-4">
        <div className="bg-white rounded-5xl p-12 shadow-card flex flex-col items-center animate-fade-in-up">
          <span className="material-symbols-outlined text-4xl animate-spin text-spark mb-4">progress_activity</span>
          <p className="font-bold text-lg">Loading Details...</p>
        </div>
      </div>
    );
  }

  if (!data || !analysis) {
    return null;
  }

  const isEtfOrIndex = (analysis.analysis_type ?? '').toLowerCase().includes('etf') || 
                       (analysis.analysis_type ?? '').toLowerCase().includes('index');

  const isPositive = (analysis.priceChangePercent ?? 0) >= 0;

  const reasons = [
    {
      title: isEtfOrIndex ? 'Portfolio Quality' : 'Technical Moat',
      label: analysis.moat_label,
      text: analysis.technical_moat,
      tone: 'opportunity' as SectionTone,
    },
    {
      title: 'Catalysts',
      label: analysis.catalysts_label,
      text: analysis.catalysts,
      tone: 'opportunity' as SectionTone,
    },
    {
      title: 'Price Asymmetry',
      label: analysis.asymmetry_label,
      text: analysis.price_asymmetry,
      tone: 'opportunity' as SectionTone,
    },
    {
      title: 'Risk Assessment',
      label: analysis.risk_label,
      text: analysis.risk_assessment,
      tone: 'opportunity' as SectionTone, // The level parsing handles 'low risk' as positive
    }
  ];

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-text-main/40 backdrop-blur-md p-4 sm:p-8 overflow-y-auto">
      <div className="bg-surface rounded-[40px] w-full max-w-[1400px] shadow-card flex flex-col my-auto relative animate-fade-in-up">
        
        {/* Header - White Background */}
        <div className="flex flex-col sm:flex-row sm:items-center justify-between p-8 md:p-10 border-b border-divider bg-white rounded-t-[40px]">
          <div className="flex flex-col sm:flex-row sm:items-center gap-6">
            <div>
              <h2 className="text-4xl font-extrabold tracking-tight text-text-main mb-1">{data.ticker}</h2>
              <p className="text-lg font-bold text-muted">{data.companyName}</p>
            </div>
            
            {/* Price Info */}
            {analysis.currentPrice != null && (
              <div className="sm:pl-6 sm:border-l border-divider mt-4 sm:mt-0">
                <div className="flex items-baseline gap-3">
                  <span className="text-4xl font-bold tabular-nums text-text-main">
                    ${analysis.currentPrice.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                  </span>
                  <span className={`text-xl font-black ${isPositive ? 'text-positive' : 'text-negative'}`}>
                    {isPositive ? '+' : ''}{(analysis.priceChangePercent ?? 0).toFixed(1)}%
                  </span>
                </div>
              </div>
            )}
          </div>

          <button 
            onClick={onClose} 
            className="w-12 h-12 rounded-full bg-divider hover:bg-surface-dim transition-colors flex items-center justify-center mt-4 sm:mt-0 absolute top-8 right-8"
          >
            <span className="material-symbols-outlined text-text-main">close</span>
          </button>
        </div>

        {/* Content */}
        <div className="p-8 md:p-10 bg-cream rounded-b-[40px]">
          <h3 className="text-xl font-extrabold mb-6 text-text-main uppercase tracking-tight text-center">Key Rating Factors</h3>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
            {reasons.map((r, idx) => (
              <div key={idx} className="bg-white rounded-4xl p-8 shadow-minimal border border-divider flex flex-col items-center text-center">
                <h4 className="text-xl font-black text-black mb-3">{r.title}</h4>
                <div className="mb-4">
                  <SectionBadge label={r.label} tone={r.tone} />
                </div>
                <p className="text-sm text-text-muted leading-relaxed flex-1">
                  {r.text || 'No detailed reasoning available.'}
                </p>
              </div>
            ))}
          </div>
        </div>

      </div>
    </div>
  );
}
