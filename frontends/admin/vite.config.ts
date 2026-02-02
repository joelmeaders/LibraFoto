/// <reference types="vitest" />
import { defineConfig } from "vite";
import angular from "@analogjs/vite-plugin-angular";
import { resolve } from "path";

export default defineConfig(({ mode }) => ({
  plugins: [angular()],
  resolve: {
    alias: {
      "@core": resolve(__dirname, "./src/app/core"),
      "@shared": resolve(__dirname, "./src/app/shared"),
      "@features": resolve(__dirname, "./src/app/features"),
      "@environments": resolve(__dirname, "./src/environments"),
    },
  },
  test: {
    globals: true,
    setupFiles: ["src/test-setup.ts"],
    environment: "jsdom",
    include: ["src/**/*.{test,spec}.{js,mjs,cjs,ts,mts,cts,jsx,tsx}"],
    reporters: ["default"],
    coverage: {
      provider: "v8",
      reporter: ["text", "json", "html"],
      include: ["src/**/*.ts"],
      exclude: [
        "src/**/*.spec.ts",
        "src/main.ts",
        "src/test-setup.ts",
        "src/**/*.module.ts",
        "src/app/app.config.ts",
        "src/app/app.routes.ts",
      ],
    },
  },
}));
