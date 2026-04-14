/** @type {import('tailwindcss').Config} */
export default {
  content: [
    "./index.html",
    "./src/**/*.{js,ts,jsx,tsx}",
  ],
  theme: {
    extend: {
      colors: {
        primary: {
          50: "#f8fbff",
          100: "#eef4ff",
          600: "#0c4a8a",
        },
        slate: {
          50: "#fcfdff",
          400: "#7a8ba8",
          600: "#55647c",
          700: "#4a5d79",
          800: "#37445a",
          900: "#1b1f2a",
        },
        border: {
          light: "#d6dfea",
          medium: "#b8c5db",
          accent: "#9eb2d2",
        },
      },
      fontFamily: {
        sans: ['"Segoe UI"', 'Tahoma', 'Geneva', 'Verdana', 'sans-serif'],
      },
    },
  },
  plugins: [],
};
