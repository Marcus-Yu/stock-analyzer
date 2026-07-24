/** @type {import('tailwindcss').Config} */
export default {
  content: ["./index.html", "./src/**/*.{js,ts,jsx,tsx}"],
  theme: {
    extend: {
      colors: {
        cream: "#FFFDF5",
        spark: "#97d5ff",
        "spark-dark": "#6bbce8",
        muted: "#7C7A72",
        divider: "#F2EFE4",
        surface: "#fbf8ff",
        "surface-dim": "#dbd9e1",
        "surface-bright": "#fbf8ff",
        "surface-container-lowest": "#ffffff",
        "surface-container-low": "#f5f2fb",
        "spark-blue": "#8E94F2",
        positive: "#22c55e",
        negative: "#ef4444",
        "text-main": "#111827",
        "text-muted": "#6b7280",
        "border-light": "#e5e7eb",
      },
      fontFamily: {
        sans: ["Inter", "system-ui", "-apple-system", "sans-serif"],
      },
      borderRadius: {
        "4xl": "2rem",
        "5xl": "2.5rem",
        "6xl": "3.75rem",
      },
      boxShadow: {
        minimal: "0 10px 40px -10px rgba(74, 73, 67, 0.08)",
        card: "0 4px 6px -1px rgba(0, 0, 0, 0.05), 0 2px 4px -1px rgba(0, 0, 0, 0.03)",
      },
    },
  },
  plugins: [],
};
