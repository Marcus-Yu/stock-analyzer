import { useEffect, useState } from 'react';

type FactorWeight = {
  id: string;
  factorName: string;
  isPredefined: boolean;
  sector: string;
  weight: number;
  reliabilityScore: number;
  updatedAt: string;
};

type Adjustment = {
  id: string;
  factorName: string;
  previousWeight: number;
  newWeight: number;
  reason: string;
  adjustedAt: string;
};

type PostMortem = {
  id: string;
  ticker: string;
  timeframe: string;
  whatWasCorrect: string;
  whatWasIncorrect: string;
  whatWasMissed: string;
  generatedAt: string;
};

type Insights = {
  activeWeights: FactorWeight[];
  recentAdjustments: Adjustment[];
  recentPostMortems: PostMortem[];
};

export function Learning() {
  const [insights, setInsights] = useState<Insights | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    fetch('/api/learning/insights')
      .then((res) => res.json())
      .then((data) => {
        setInsights(data);
        setLoading(false);
      })
      .catch((err) => {
        console.error(err);
        setLoading(false);
      });
  }, []);

  if (loading) return (
    <div className="flex flex-col items-center justify-center py-32 text-text-muted">
      <span className="material-symbols-outlined text-5xl animate-spin mb-4">progress_activity</span>
      <p className="text-lg font-bold">Loading Learning Engine Insights...</p>
    </div>
  );
  
  if (!insights) return <div className="p-16 text-center font-bold text-negative">Error loading insights.</div>;

  return (
    <div className="px-6 md:px-16 py-12 space-y-16 max-w-screen-2xl mx-auto">
      <div>
        <h1 className="text-5xl font-black text-text-main mb-4 tracking-tighter">Autonomous Learning</h1>
        <p className="text-xl text-text-muted font-medium">Real-time model tracking and post-mortem evaluation data.</p>
      </div>

      <section>
        <h2 className="text-3xl font-black text-text-main mb-8 tracking-tight">Active Factor Weights</h2>
        <div className="grid md:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-6">
          {insights.activeWeights.map((w) => (
            <div key={w.id} className="bg-white p-6 rounded-3xl shadow-sm border border-border-light flex flex-col justify-between hover:shadow-md transition-shadow">
              <div>
                <div className="flex justify-between items-start mb-2">
                  <h3 className="font-bold text-lg text-text-main leading-tight mr-2">{w.factorName}</h3>
                  {w.isPredefined ? (
                    <span className="bg-blue-100 text-blue-800 text-[10px] uppercase tracking-wider px-2 py-1 rounded-full font-bold flex-shrink-0">Base</span>
                  ) : (
                    <span className="bg-purple-100 text-purple-800 text-[10px] uppercase tracking-wider px-2 py-1 rounded-full font-bold flex-shrink-0">AI Found</span>
                  )}
                </div>
                <div className="text-sm text-text-muted mb-6 flex justify-between items-center bg-surface-container-low px-3 py-2 rounded-xl">
                  <span className="font-medium text-xs uppercase tracking-wider">Reliability</span>
                  <span className="font-bold text-text-main">{w.reliabilityScore}/100</span>
                </div>
              </div>
              <div className="mt-auto">
                <div className="text-xs font-bold text-text-muted mb-1 uppercase tracking-widest">Current Weight</div>
                <div className="text-4xl font-black text-spark-blue">{(w.weight).toFixed(3)}</div>
              </div>
            </div>
          ))}
        </div>
      </section>

      <div className="grid lg:grid-cols-2 gap-12 lg:gap-24">
        <section>
          <h2 className="text-3xl font-black text-text-main mb-8 tracking-tight">Recent Post-Mortems</h2>
          <div className="space-y-6">
            {insights.recentPostMortems.length === 0 ? (
              <div className="text-text-muted/50 flex flex-col items-center py-12">
                <span className="material-symbols-outlined text-4xl mb-3">auto_awesome</span>
                <span className="font-semibold text-lg">No post-mortems available yet.</span>
                <span className="text-sm mt-1">Models automatically evaluate when predictions expire.</span>
              </div>
            ) : (
              insights.recentPostMortems.map((p) => (
                <div key={p.id} className="bg-white p-8 rounded-3xl shadow-sm border border-border-light">
                  <div className="flex items-center gap-3 mb-6 pb-4 border-b border-border-light">
                    <span className="font-black text-2xl tracking-tight">{p.ticker}</span>
                    <span className="text-xs font-bold bg-surface-container-low px-3 py-1 rounded-full text-text-muted uppercase tracking-wider">{p.timeframe}</span>
                    <span className="text-xs font-medium text-text-muted ml-auto">{new Date(p.generatedAt).toLocaleDateString()}</span>
                  </div>
                  <div className="space-y-6">
                    {p.whatWasCorrect && (
                      <div>
                        <div className="text-xs font-bold tracking-widest uppercase text-positive mb-2 flex items-center gap-2">
                          <span className="material-symbols-outlined text-[16px]">check_circle</span>
                          What Went Right
                        </div>
                        <p className="text-sm text-text-main leading-relaxed">{p.whatWasCorrect}</p>
                      </div>
                    )}
                    {p.whatWasIncorrect && (
                      <div>
                        <div className="text-xs font-bold tracking-widest uppercase text-negative mb-2 flex items-center gap-2">
                          <span className="material-symbols-outlined text-[16px]">cancel</span>
                          What Went Wrong
                        </div>
                        <p className="text-sm text-text-main leading-relaxed">{p.whatWasIncorrect}</p>
                      </div>
                    )}
                    {p.whatWasMissed && (
                      <div>
                        <div className="text-xs font-bold tracking-widest uppercase text-orange-500 mb-2 flex items-center gap-2">
                          <span className="material-symbols-outlined text-[16px]">warning</span>
                          Blind Spots
                        </div>
                        <p className="text-sm text-text-main leading-relaxed">{p.whatWasMissed}</p>
                      </div>
                    )}
                  </div>
                </div>
              ))
            )}
          </div>
        </section>

        <section>
          <h2 className="text-3xl font-black text-text-main mb-8 tracking-tight">Weight Adjustments</h2>
          <div className="space-y-4">
            {insights.recentAdjustments.length === 0 ? (
              <div className="text-text-muted/50 flex flex-col items-center py-12">
                <span className="material-symbols-outlined text-4xl mb-3">tune</span>
                <span className="font-semibold text-lg">No adjustments recorded.</span>
              </div>
            ) : (
              insights.recentAdjustments.map((a) => {
                const diff = a.newWeight - a.previousWeight;
                const isPositive = diff >= 0;
                return (
                  <div key={a.id} className="bg-white p-5 rounded-2xl shadow-sm border border-border-light flex items-start gap-5">
                    <div className={`mt-1 flex-shrink-0 flex items-center justify-center w-10 h-10 rounded-full ${isPositive ? 'bg-green-100 text-green-700' : 'bg-red-100 text-red-700'}`}>
                      <span className="material-symbols-outlined text-lg">{isPositive ? 'trending_up' : 'trending_down'}</span>
                    </div>
                    <div className="w-full">
                      <div className="flex justify-between items-center mb-1">
                        <span className="font-bold text-text-main text-lg">{a.factorName}</span>
                        <span className="text-[10px] font-bold text-text-muted uppercase tracking-wider">{new Date(a.adjustedAt).toLocaleString()}</span>
                      </div>
                      <div className="text-sm text-text-muted mb-3 flex items-center gap-2">
                        <span className="font-medium bg-surface-container-low px-2 py-0.5 rounded">{a.previousWeight.toFixed(3)}</span>
                        <span className="material-symbols-outlined text-sm">arrow_right_alt</span>
                        <span className={`font-bold px-2 py-0.5 rounded ${isPositive ? 'bg-green-50 text-positive' : 'bg-red-50 text-negative'}`}>{a.newWeight.toFixed(3)}</span>
                      </div>
                      <p className="text-xs text-text-muted/80 bg-surface-container-low p-3 rounded-xl italic">{a.reason}</p>
                    </div>
                  </div>
                );
              })
            )}
          </div>
        </section>
      </div>
    </div>
  );
}
