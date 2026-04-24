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
        surface: "#1b1b21",
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
      },
    },
  },
  plugins: [],
};
