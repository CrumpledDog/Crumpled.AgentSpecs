export const manifests: Array<UmbExtensionManifest> = [
  {
    type: "workspaceView",
    alias: "Crumpled.AgentSpecs.WorkspaceView",
    name: "Crumpled Agent Specs Workspace View",
    element: () => import("./agent-specs-workspace-view.element.js"),
    weight: 50,
    meta: {
      label: "Agent Specs",
      pathname: "agent-specs",
      icon: "icon-code",
    },
    conditions: [
      {
        alias: "Umb.Condition.WorkspaceAlias",
        match: "Umb.Workspace.Document",
      },
    ],
  },
];
