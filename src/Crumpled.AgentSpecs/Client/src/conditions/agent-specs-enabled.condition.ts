import type { UmbConditionConfigBase } from "@umbraco-cms/backoffice/extension-api";
import { UmbConditionBase } from "@umbraco-cms/backoffice/extension-registry";
import { UMB_PROPERTY_STRUCTURE_WORKSPACE_CONTEXT } from "@umbraco-cms/backoffice/content-type";
import { getSettings } from "../api/index.js";

export const AGENT_SPECS_ENABLED_CONDITION_ALIAS = "Crumpled.AgentSpecs.Condition.Enabled";

// The excluded document types are site settings, so they are read once per backoffice session. If they can't be
// read, nothing is excluded: a settings hiccup must not hide the view.
let excludedDocumentTypes: Promise<Array<string>> | undefined;

function loadExcludedDocumentTypes(): Promise<Array<string>> {
  excludedDocumentTypes ??= getSettings()
    .then(({ data }) => data?.excludedDocumentTypes ?? [])
    .catch(() => []);
  return excludedDocumentTypes;
}

/**
 * Permits the Agent Specs view unless the document's own document type is listed in
 * Crumpled:AgentSpecs:ExcludedDocumentTypes. Compositions are not considered.
 */
export class AgentSpecsEnabledCondition extends UmbConditionBase<UmbConditionConfigBase> {
  constructor(host: ConstructorParameters<typeof UmbConditionBase<UmbConditionConfigBase>>[0], args: ConstructorParameters<typeof UmbConditionBase<UmbConditionConfigBase>>[1]) {
    super(host, args);

    this.consumeContext(UMB_PROPERTY_STRUCTURE_WORKSPACE_CONTEXT, (context) => {
      this.observe(
        context?.structure.ownerContentTypeAlias,
        async (alias) => {
          if (!alias) {
            // Not known yet (the document is still loading): stay hidden rather than flash the tab.
            this.permitted = false;
            return;
          }

          const excluded = await loadExcludedDocumentTypes();
          this.permitted = !excluded.some((item) => item.toLowerCase() === alias.toLowerCase());
        },
        "agentSpecsOwnerAlias",
      );
    });
  }
}

export const manifests: Array<UmbExtensionManifest> = [
  {
    type: "condition",
    name: "Crumpled Agent Specs Enabled Condition",
    alias: AGENT_SPECS_ENABLED_CONDITION_ALIAS,
    api: AgentSpecsEnabledCondition,
  },
];
