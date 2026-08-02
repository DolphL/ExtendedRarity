# Extended Rarity

A BepInEx mod for **Ravenfield** that adds two new rarity tiers on top of the vanilla Common / Uncommon / Rare system:

- **EPIC** — red
- **MYTHIC** — purple

Both tiers are fully integrated: paint any vehicle, turret or weapon with them in the loadout menu, and they will spawn in battle with configurable upgrade chances, save/load correctly, and show proper colored labels on entry cards.

![tiers](docs/screenshot.png)

## Features

- Two new paintable rarity tiers with their own circles in the painting panel (hotkeys **5** and **6**)
- Configurable colors, labels and spawn chances (`BepInEx/config/com.dolph.extendedrarity.cfg`)
- Rarity ladder: `Common -> Uncommon -> Rare -> Epic -> Mythic`; each step up is a separate roll, so higher tiers are progressively rarer
- Fixes the vanilla fallback cap so vehicles painted with a new tier still spawn even when they are the only vehicle in their slot
- Debug tools: per-spawn tier roll logging and force-roll test modes
- Fail-safes: the plugin verifies the enum extension on startup and refuses to activate half-broken state

## Installation

1. Install [BepInEx 5.4.23.x (x64)](https://github.com/BepInEx/BepInEx/releases) into the Ravenfield folder and run the game once. **BepInEx 6 is not supported.**
2. Download the latest `ExtendedRarity-x.y.z.zip` from [Releases](../../releases).
3. Drop the contents into the game folder so that:
   - `ExtendedRarity.Patcher.dll` ends up in `Ravenfield/BepInEx/patchers/`
   - `ExtendedRarity.dll` ends up in `Ravenfield/BepInEx/plugins/`
4. Launch the game. The config file is generated on first run.

To verify, check `BepInEx/LogOutput.log` for:

```
[ExtendedRarity.Patcher] Added value Epic = 3 to enum RarityTier.
[ExtendedRarity.Patcher] Added value Mythic = 4 to enum RarityTier.
[ExtendedRarity] Tier system extended: Common, Uncommon, Rare, Epic, Mythic. ...
```

## Configuration

| Section | Key | Default | Description |
|---|---|---|---|
| Epic | Color | `#E62626` | Epic tier color |
| Epic | Label | `EPIC` | Card label |
| Epic | VehicleUpgradeChance | `0.15` | Rare -> Epic chance on vehicle spawn |
| Epic | WeaponUpgradeChance | `0.10` | Rare -> Epic chance for bot weapons |
| Mythic | Color | `#A020F0` | Mythic tier color |
| Mythic | Label | `MYTHIC` | Card label |
| Mythic | VehicleUpgradeChance | `0.10` | Epic -> Mythic chance on vehicle spawn |
| Mythic | WeaponUpgradeChance | `0.05` | Epic -> Mythic chance for bot weapons |
| Debug | SpawnDebug | `false` | Log every tier roll |
| Debug | ForceEpicRolls / ForceMythicRolls | `false` | Test modes: force every roll to the given tier |

With default settings, a Mythic vehicle is roughly a 1-in-200 spawn across a typical map, and a Mythic weapon on a regular bot is about 1-in-4000 — tune the chances to taste.

## How it works

The interesting part of this mod is *how* the tiers are added. The game caches `Enum.GetValues(typeof(RarityTier))` into a `static readonly` field and builds every rarity pool from it. That field cannot be patched at runtime in Ravenfield's Mono build (no Reflection.Emit, silently ignored reflection writes, and JIT inlining defeats method patches — see the commit history of this repo for the war story). So instead:

- **`ExtendedRarity.Patcher.dll`** is a BepInEx *preloader patcher*: it runs before `Assembly-CSharp.dll` is loaded and injects `Epic = 3` and `Mythic = 4` directly into the `RarityTier` enum with Mono.Cecil. From the game's point of view the tiers have always existed.
- **`ExtendedRarity.dll`** is a regular BepInEx plugin: it extends the color table and the "roll a higher tier" chance tables, adds the painting UI circles and hotkeys, patches entry card labels, and lifts the inlined `HighestRarity = Rare` fallback cap in vehicle spawning.

## Building from source

1. Copy the reference DLLs listed in [`libs/README.md`](libs/README.md) into `libs/`.
2. `dotnet build -c Release src/ExtendedRarity.Patcher` and `dotnet build -c Release src/ExtendedRarity`.

## Compatibility

- Ravenfield (Unity 2020.3, Mono) with BepInEx 5.4.23.x
- Works alongside Steam Workshop mutators and content mods
- Known limitation: the specialized bot weapon fallback path retries at Rare (inlined constant), so Epic/Mythic specialized weapons only appear on a direct high-tier roll

## License

MIT

## Credits

Designed, tested and maintained by [Dolph](https://github.com/DolphL). Code written with AI assistance (Claude).
