export const manifests: Array<UmbExtensionManifest> = [
  {
    name: "Crumpled Package Template Dashboard",
    alias: "Crumpled.AgentSpecs.Dashboard",
    type: "dashboard",
    js: () => import("./dashboard.element.js"),
    meta: {
      label: "Crumpled Package Template",
      pathname: "crumpled-agent-specs",
    },
    conditions: [
      {
        alias: "Umb.Condition.SectionAlias",
        match: "Umb.Section.Settings",
      },
    ],
  },
];
