export const manifests: Array<UmbExtensionManifest> = [
  {
    name: "Crumpled Package Template Entrypoint",
    alias: "Crumpled.AgentSpecs.Entrypoint",
    type: "backofficeEntryPoint",
    js: () => import("./entrypoint.js"),
  },
];
