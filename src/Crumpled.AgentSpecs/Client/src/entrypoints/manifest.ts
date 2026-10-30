export const manifests: Array<UmbExtensionManifest> = [
  {
    name: "Crumpled Agent Specs Entrypoint",
    alias: "Crumpled.AgentSpecs.Entrypoint",
    type: "backofficeEntryPoint",
    js: () => import("./entrypoint.js"),
  },
];
