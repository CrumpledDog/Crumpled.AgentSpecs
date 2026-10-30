# Crumpled.AgentSpecs

TODO: one paragraph describing what this package does and why it exists.

<img src="https://raw.githubusercontent.com/CrumpledDog/Crumpled.AgentSpecs/develop/v1/icons/icon.png" width="120" height="120" alt="">

## Installation

```bash
dotnet add package Crumpled.AgentSpecs
```

TODO: describe configuration, e.g.:

```json
{
  "Crumpled": {
    "CrumpledAgentSpecs": {
      "Enabled": true
    }
  }
}
```

(the nested config section name above will read `CrumpledAgentSpecs` after generation - most existing Crumpled packages drop the repeated `Crumpled` prefix here, e.g. `Crumpled:VerifyOwnership` - rename it to match that convention when you fill in this TODO)

## How it works

TODO.

## Backoffice UI

TODO - or delete this section if generated with `--include-client:false`.

## Requirements

Each Umbraco major has its own package version line, so pick the one that matches your site:

| Package version | Umbraco CMS | Branch |
|---|---|---|
| `1.x` | 17 | `release/v1` |
| `2.x` | 18 | `release/v2` |

```bash
# Umbraco 17
dotnet add package Crumpled.AgentSpecs --version "1.*"

# Umbraco 18
dotnet add package Crumpled.AgentSpecs --version "2.*"
```

TODO: delete the `2.x` row (and the Umbraco 18 command) until a `develop/v2` line actually exists.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for the branching strategy and formatting requirements.

## License

[MIT](LICENSE)
