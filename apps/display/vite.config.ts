import { defineConfig, loadEnv } from "vite";

export default defineConfig(({ mode }) => {
  // Load env file based on `mode` in the current working directory.
  const env = loadEnv(mode, process.cwd(), "");

  // Determine API target from environment or use default
  const apiTarget = env.VITE_API_TARGET || "http://localhost:5179";

  return {
    root: ".",
    build: {
      outDir: "dist",
      emptyOutDir: true,
      sourcemap: mode === "development",
      // Minimize bundle size for Raspberry Pi
      minify: mode === "production" ? "terser" : false,
      terserOptions: {
        compress: {
          drop_console: mode === "production",
        },
      },
      // Optimize chunk size
      rollupOptions: {
        output: {
          manualChunks: undefined, // Keep everything in one chunk for simplicity
        },
      },
    },
    server: {
      port: 3000,
      host: true, // Listen on all addresses
      proxy: {
        "/api": {
          target: apiTarget,
          changeOrigin: true,
          // xfwd: true automatically adds X-Forwarded-Host, X-Forwarded-For, X-Forwarded-Proto
          // This enables the backend to know the real client IP/host for QR code URL generation
          xfwd: true,
        },
      },
    },
    preview: {
      port: 3000,
      host: true,
    },
    // Define global constants
    define: {
      __APP_VERSION__: JSON.stringify(
        process.env.npm_package_version || "1.0.0",
      ),
    },
  };
});
