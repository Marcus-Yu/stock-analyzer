import { useEffect, useState } from 'react';
import { PredictionDetailModal } from '../components/PredictionDetailModal';

interface DashboardStats {
  totalPredictions: number;
  evaluatedCount: number;
  overallAccuracyPercent: number;
  accurateCount: number;
  partialCount: number;
  inaccurateCount: number;
  horizonAccuracy: Record<string, { count: number; accuracyPercent: number }>;
}

interface Prediction {
  id: string;
  ticker: string;
  companyName: string;
  timestamp: string;
  predictionScore: number;
  recommendation: string;
  dataConfidenceScore: number;
  isEtf: boolean;
}

export function Predictions() {
  const [stats, setStats] = useState<DashboardStats | null>(null);
  const [predictions, setPredictions] = useState<Prediction[]>([]);
  const [loading, setLoading] = useState(true);
  const [selectedId, setSelectedId] = useState<string | null>(null);

  useEffect(() => {
    const fetchData = async () => {
      try {
        const statsRes = await fetch('http://localhost:5159/api/predictions/dashboard');
        const statsData = await statsRes.json();
        setStats(statsData);

        const listRes = await fetch('http://localhost:5159/api/predictions');
        const listData = await listRes.json();
        setPredictions(listData);
      } catch (error) {
        console.error('Failed to fetch predictions data', error);
      } finally {
        setLoading(false);
      }
    };
    fetchData();
  }, []);

  if (loading) return <div className="p-10 text-center text-text-muted">Loading Predictions...</div>;

  // Group predictions by date and asset type
  const groupedPredictions = predictions.reduce<Record<string, { stocks: Prediction[], etfs: Prediction[] }>>((acc, p) => {
    const dateStr = new Date(p.timestamp).toLocaleDateString('en-US', {
      timeZone: 'America/Toronto',
      month: 'long',
      day: 'numeric',
      year: 'numeric'
    });
    if (!acc[dateStr]) acc[dateStr] = { stocks: [], etfs: [] };
    
    const etfRegex = /\b(etf|index|fund|ishares|vanguard|spdr|invesco|ark|schwab|select sector|trust)\b/i;
    const isEtfFallback = p.isEtf || etfRegex.test(p.companyName || '');
    
    const group = isEtfFallback ? acc[dateStr].etfs : acc[dateStr].stocks;
    // Deduplicate by ticker within the same day (keeps the most recent since list is sorted desc)
    if (!group.some(existing => existing.ticker === p.ticker)) {
      group.push(p);
    }
    return acc;
  }, {});

  // Sort each group by score descending
  Object.values(groupedPredictions).forEach(day => {
    day.stocks.sort((a, b) => b.predictionScore - a.predictionScore);
    day.etfs.sort((a, b) => b.predictionScore - a.predictionScore);
  });

  const totalDisplayed = Object.values(groupedPredictions).reduce((sum, day) => sum + day.stocks.length + day.etfs.length, 0);

  const renderCard = (p: Prediction) => {
    const score = p.predictionScore;
    const offset = 176 - (176 * score / 100);
    
    let recColorClass = "text-text-main border-border-light bg-surface-dim";
    if (p.recommendation.toLowerCase().includes("buy")) recColorClass = "text-positive border-positive/30 bg-positive/10";
    if (p.recommendation.toLowerCase().includes("sell")) recColorClass = "text-negative border-negative/30 bg-negative/10";
    if (p.recommendation.toLowerCase().includes("hold")) recColorClass = "text-yellow-600 border-yellow-600/30 bg-yellow-600/10";

    return (
      <article 
        key={p.id} 
        className="bg-white rounded-3xl p-6 sm:p-8 shadow-card border border-border-light flex flex-col justify-between min-h-[220px] cursor-pointer hover:-translate-y-1 transition-all duration-300" 
        data-purpose="stock-card"
        onClick={() => setSelectedId(p.id)}
      >
        <div className="flex justify-between items-start">
          <div className="pr-4">
            <h2 className="text-3xl font-extrabold tracking-tight mb-1">{p.ticker}</h2>
            <p className="text-xs text-text-muted font-semibold tracking-wider uppercase leading-relaxed break-words" title={p.companyName}>
              {p.companyName}
            </p>
          </div>
        </div>
        
        <div className="flex justify-between items-center mt-4">
          <div className="flex flex-col items-start justify-center h-full">
            <p className={`text-base sm:text-lg font-bold ${recColorClass} border-2 rounded-xl px-4 py-1.5 inline-block leading-tight text-center mb-1`}>{p.recommendation}</p>
            <p className="text-[10px] text-text-muted font-medium mt-1">Recommendation</p>
          </div>
          <div className="flex flex-col items-center justify-center pl-8 sm:pl-12 border-l border-border-light h-full min-h-[100px]">
            <div className="relative flex items-center justify-center w-24 h-24">
              <svg className="w-full h-full transform -rotate-90" viewBox="0 0 64 64">
                <circle cx="32" cy="32" r="28" fill="transparent" strokeWidth="4" className="stroke-divider" />
                <circle cx="32" cy="32" r="28" fill="transparent" strokeWidth="4" className="stroke-spark" strokeDasharray="176" strokeDashoffset={offset} strokeLinecap="round" />
              </svg>
              <div className="absolute inset-0 flex items-center justify-center">
                <span className="font-extrabold text-3xl text-[#97d5ff]">{score}</span>
              </div>
            </div>
            <span className="text-[10px] font-black tracking-widest mt-2 uppercase text-spark-dark">Score</span>
          </div>
        </div>
      </article>
    );
  };

  return (
    <div className="p-6 md:p-10 lg:p-16">
      {/* Header Section */}
      <header className="mb-10 max-w-7xl mx-auto">
        <h1 className="text-4xl md:text-5xl font-extrabold tracking-tight mb-2">Predictions</h1>
        <p className="text-text-muted mt-2 font-medium">
          Total: {totalDisplayed} | Evaluated: {stats?.evaluatedCount || 0} | Accuracy: {stats?.overallAccuracyPercent || 0}%
        </p>
      </header>

      {/* Main Content Grid */}
      <main className="max-w-7xl mx-auto space-y-12">
        {Object.entries(groupedPredictions).map(([date, { stocks, etfs }]) => (
          <section key={date} className="pb-8">
            <div className="mb-10 flex items-baseline gap-4">
              <h2 className="text-3xl font-extrabold text-text-main">{date}</h2>
              <span className="text-sm font-bold text-sky-600 uppercase tracking-widest px-3 py-1 bg-sky-50 rounded-lg border border-sky-200">{stocks.length} {stocks.length === 1 ? 'Stock' : 'Stocks'}</span>
              <span className="text-sm font-bold text-indigo-600 uppercase tracking-widest px-3 py-1 bg-indigo-50 rounded-lg border border-indigo-200">{etfs.length} {etfs.length === 1 ? 'Index/ETF' : 'Indexes/ETFs'}</span>
            </div>

            {stocks.length > 0 && (
              <div className="mb-12">
                <h3 className="text-xl font-bold text-text-muted mb-6 uppercase tracking-widest">Stocks</h3>
                <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-8">
                  {stocks.map(renderCard)}
                </div>
              </div>
            )}

            {etfs.length > 0 && (
              <div>
                <h3 className="text-xl font-bold text-text-muted mb-6 uppercase tracking-widest">Indexes & ETFs</h3>
                <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-8">
                  {etfs.map(renderCard)}
                </div>
              </div>
            )}
          </section>
        ))}
      </main>

      {selectedId && (
        <PredictionDetailModal 
          predictionId={selectedId} 
          onClose={() => setSelectedId(null)} 
        />
      )}
    </div>
  );
}
