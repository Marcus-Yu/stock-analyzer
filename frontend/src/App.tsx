import { BrowserRouter, Routes, Route, Link } from 'react-router-dom';
import { Dashboard } from './pages/Dashboard';
import { Predictions } from './pages/Predictions';
import { Learning } from './pages/Learning';

function NavBar() {
  return (
    <nav className="bg-white border-b border-border-light shadow-sm">
      <div className="max-w-7xl mx-auto px-6 md:px-10 lg:px-16">
        <div className="flex items-center justify-between h-16">
          <div className="flex items-center space-x-6">
            <span className="font-extrabold text-2xl tracking-tight text-spark-dark">
              Spark
            </span>
            <Link to="/" className="text-sm font-semibold text-text-muted hover:text-text-main transition-colors uppercase tracking-widest mt-1">
              Home
            </Link>
            <Link to="/predictions" className="text-sm font-semibold text-text-muted hover:text-text-main transition-colors uppercase tracking-widest mt-1">
              Predictions
            </Link>
            <Link to="/learning" className="text-sm font-semibold text-text-muted hover:text-text-main transition-colors uppercase tracking-widest mt-1">
              Learning
            </Link>
          </div>
          <div className="flex items-center space-x-4">
             {/* Placeholders for notifications and profile from screenshot */}
             <div className="w-8 h-8 rounded-full flex items-center justify-center text-text-muted hover:bg-gray-100 cursor-pointer">
               <span className="material-symbols-outlined text-xl">notifications</span>
             </div>
             <div className="w-10 h-10 rounded-full bg-[#f2efe4] flex items-center justify-center text-text-muted hover:bg-[#e5e2d6] cursor-pointer">
               <span className="material-symbols-outlined text-xl">person</span>
             </div>
          </div>
        </div>
      </div>
    </nav>
  );
}

function App() {
  return (
    <BrowserRouter>
      <div className="min-h-screen flex flex-col bg-cream font-sans text-text-main">
        <NavBar />
        <div className="flex-1">
          <Routes>
            <Route path="/" element={<Dashboard />} />
            <Route path="/predictions" element={<Predictions />} />
            <Route path="/learning" element={<Learning />} />
          </Routes>
        </div>
      </div>
    </BrowserRouter>
  );
}

export default App;
