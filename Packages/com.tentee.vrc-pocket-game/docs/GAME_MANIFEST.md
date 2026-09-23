# Game catalog manifest v1 (draft)

This is a proposed, **optional** JSON manifest for a public catalog of games built with the Pocket Game SDK. The [JSON Schema](game-manifest.schema.json) defines the machine-readable shape; [the sample](game-manifest.example.json) illustrates a complete entry. Neither the SDK runtime nor the installer reads this file. Adding one does not submit or publish a game.

Put one `pocket-game.json` at the root of each game project or package. A future catalog importer can discover that file from an explicitly submitted repository URL and validate it against a pinned schema version. The included counter manifest is an example of the format, **not** a catalog submission. A catalog should store the source repository and commit alongside each imported record so that images, edits, and removals can be traced.

## Fields and filter behavior

| Field | Purpose |
| --- | --- |
| `schemaVersion` | Integer format version, initially `1`. A future breaking change increments this separately from the SDK version. |
| `id` | Permanent catalog identity, for example `author.pocket.game`. Keep it when a game is renamed or updated. A catalog rejects duplicate IDs across submissions. |
| `version` | Version of the game content, not the SDK. |
| `defaultLocale`, `localizations` | Catalog display text with a fallback. Each localization has a name and short summary, with an optional longer description. The default locale must exist in `localizations`. |
| `languages` | Languages actually available **inside the game**. A translated catalog description alone does not add a gameplay language. Filter on these tags. |
| `genres` | One to three values from the controlled v1 vocabulary. This is the dependable genre filter; catalog UI translates their labels. |
| `tags` | Optional narrow, lowercase keywords for search or exploratory filters. They are less stable than genres and may be normalized before publication. |
| `playModes`, `inputModes`, `platforms` | Optional filters for player interaction, controls, and tested VRChat world builds. Omit unknown or untested claims rather than treating an absent field as `false`. |
| `author`, `links` | Creator credit and a required HTTPS project page; optional install and VRChat world pages. `worldUrl` is for a playable world, not an arbitrary test instance. |
| `media` | Optional HTTPS icon, cover, and trailer URLs. Remote media should be checked and proxied or cached by the publishing site. |
| `sdkCompatibility` | Optional minimum and tested SDK versions. It is informational and does not replace VPM dependencies. |
| `installation.recommendedPoolSize` | Optional starting point for world authors. The actual pool count remains a per-world choice. |

Locale values use BCP 47 tags such as `en`, `ja`, and `pt-BR`. The schema checks a practical syntax subset; an importer should canonicalize and validate tags using a BCP 47 library, check that `defaultLocale` has an entry, and reject case-insensitive duplicates. On the site, select the visitor's exact locale, then its base language if present, then `defaultLocale`. Search should index all localized names and summaries while displaying the selected localization.

The example has Japanese catalog text but lists only English under `languages`, illustrating that these are different claims.

`platforms` uses the VRChat upload targets `windows`, `android`, and `ios`; it makes no promise about every device in a target. `inputModes` describes game controls separately, so a Windows world can support both desktop and VR input. A catalog should treat these as author claims until verified. See [VRChat's platform documentation](https://creators.vrchat.com/platforms/).

## Boundaries and future catalog ingestion

- **World deployment:** actual pool size, kiosk position, scene references, and terminal profile depend on the world. They stay in the installer or a separate world configuration. `recommendedPoolSize` is only advice; it must never replace the installed pool count.
- **Persistence:** `id` is not a PlayerData key or migration switch. Keep the save namespace and its schema under game control, so changing public metadata cannot erase progress.
- **Publication:** do not put an `approved`, `featured`, ranking, install count, or moderation status in a game-authored manifest. The catalog owns those fields. Do not infer that a repository containing this file is public, endorsed, or installable.
- **URLs and assets:** use HTTPS URLs in the manifest so a static website can read an entry without knowing Unity asset paths. An importer must allowlist expected protocols, bound downloaded media size/type, sanitize displayed text, and avoid rendering raw HTML from descriptions.
- **Compatibility:** extra fields are rejected in v1 to catch spelling mistakes. Catalog ingestion should pin a schema version and add explicit migration logic when v2 appears; it should not silently reinterpret older entries.
- **Dates and availability:** a catalog should derive source update times from the submitted revision and determine listing availability itself, instead of trusting author-supplied timestamps or status flags.
- **Optional future fields:** content warnings, accessibility, estimated session length, localization coverage, and richer installation instructions may be added after real catalog submissions establish a clear need and vocabulary. They are deliberately absent from v1.

## Author and agent checklist

1. Copy [the example](game-manifest.example.json) to your game's `pocket-game.json`, keeping only claims you can support. Replace the sample's ID, author, text, and project URL.
2. Keep `id` stable for the life of the game. Change `version` when the game changes; change `schemaVersion` only when adopting a new manifest format. Do not derive either from `saveNamespace` or a pool slot.
3. Describe game language support in `languages` and catalog translations in `localizations` independently. Use controlled `genres` first and a small number of specific `tags` second.
4. Validate against [the schema](game-manifest.schema.json), then verify HTTPS links, media ownership, platform claims, and that `defaultLocale` exists. Leave uncertain optional fields out.
5. Ask the catalog maintainer how to submit the repository. No automatic discovery or submission flow is defined by this draft.
