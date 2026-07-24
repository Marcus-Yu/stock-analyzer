import { Link } from 'react-router-dom';

export function Header() {
  return (
    <header className="sticky top-0 z-30 w-full bg-cream/60 backdrop-blur-xl px-6 md:px-12 py-6 flex justify-between items-center max-w-screen-2xl mx-auto">
      <div className="flex items-center space-x-12">
        <Link to="/" className="text-2xl font-black text-spark tracking-tighter hover:opacity-80 transition-opacity">Spark</Link>
        <Link to="/predictions" className="text-sm font-bold text-muted hover:text-surface transition-colors uppercase tracking-widest">Predictions</Link>
      </div>
      <div className="flex items-center space-x-6">
        <button className="text-muted hover:text-surface transition-colors">
          <span className="material-symbols-outlined text-2xl">notifications</span>
        </button>
        <div className="w-10 h-10 rounded-full bg-divider flex items-center justify-center">
          <span className="material-symbols-outlined text-muted">person</span>
        </div>
      </div>
    </header>
  );
}
