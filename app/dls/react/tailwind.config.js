/** Rover tokens are shared by every React surface; tool code owns only layout. */
module.exports = {
  darkMode: ["selector", '[data-theme="dark"]'],
  content: ["./dls/react/**/*.{ts,tsx}", "./tools/*/react/**/*.{ts,tsx}"],
  theme: {
    screens: require("./breakpoints.cjs"),
    extend: {
      fontFamily: { sans: ["Roboto", "Arial", "sans-serif"] },
      colors: {
        border: "hsl(var(--rover-border))",
        input: "hsl(var(--rover-input))",
        ring: "hsl(var(--rover-ring))",
        background: "hsl(var(--rover-background))",
        foreground: "hsl(var(--rover-foreground))",
        card: {
          DEFAULT: "hsl(var(--rover-card))",
          foreground: "hsl(var(--rover-card-foreground))",
        },
        popover: {
          DEFAULT: "hsl(var(--rover-popover))",
          foreground: "hsl(var(--rover-popover-foreground))",
        },
        primary: {
          DEFAULT: "hsl(var(--rover-primary))",
          foreground: "hsl(var(--rover-primary-foreground))",
        },
        secondary: {
          DEFAULT: "hsl(var(--rover-secondary))",
          foreground: "hsl(var(--rover-secondary-foreground))",
        },
        muted: {
          DEFAULT: "hsl(var(--rover-muted))",
          foreground: "hsl(var(--rover-muted-foreground))",
        },
        accent: {
          DEFAULT: "hsl(var(--rover-accent))",
          foreground: "hsl(var(--rover-accent-foreground))",
        },
        destructive: {
          DEFAULT: "hsl(var(--rover-destructive))",
          foreground: "hsl(var(--rover-destructive-foreground))",
        },
        chart: { teal: "#14b8a6", orange: "#f97316", navy: "#1e293b", emerald: "#10b981" },
      },
      borderRadius: {
        lg: "var(--rover-radius)",
        md: "calc(var(--rover-radius) - 2px)",
        sm: "calc(var(--rover-radius) - 4px)",
        pill: "9999px",
        card: "1.25rem",
      },
      boxShadow: {
        "soft-card": "0 4px 20px -2px rgba(0,0,0,0.03),0 2px 6px -1px rgba(0,0,0,0.02)",
        "pill-inset": "inset 0 1px 2px rgba(0,0,0,0.04)",
      },
    },
  },
  plugins: [],
};
