import { defineConfig } from "vite";
import { svelte } from "@sveltejs/vite-plugin-svelte";
import { viteStaticCopy } from "vite-plugin-static-copy";

export default defineConfig({
  plugins: [
    svelte(),
    // Serve _framework/ (Mono WASM runtime + app assemblies) from the WASM publish output.
    // Run build-wasm.ps1 first to generate wasm-dist/AppBundle/_framework/.
    // In dev mode: served directly from source (no copy needed).
    // In build mode: copied into dist/_framework/.
    viteStaticCopy({
      targets: [
        {
          src: "../wasm-dist/AppBundle/_framework",
          dest: "",
        },
      ],
    }),
  ],
  server: { port: 5175, strictPort: true },
});
