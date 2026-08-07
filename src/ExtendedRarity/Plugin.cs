using System;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace ExtendedRarity
{
    [BepInPlugin("com.dolph.extendedrarity", "Extended Rarity", "1.3.0")]
    public class Plugin : BaseUnityPlugin
    {
        internal const RarityTier Epic = (RarityTier)3;
        internal const RarityTier Mythic = (RarityTier)4;
        internal const RarityTier TopTier = Mythic;
        internal const int ExpectedTierCount = 5;

        internal static BepInEx.Logging.ManualLogSource Log;

        internal static ConfigEntry<string> EpicColorHex, MythicColorHex;
        internal static ConfigEntry<string> EpicLabel, MythicLabel;
        internal static ConfigEntry<float> EpicVehicleChance, EpicWeaponChance;
        internal static ConfigEntry<float> MythicVehicleChance, MythicWeaponChance;
        internal static ConfigEntry<float> VehicleUncommonChance, VehicleRareChance, WeaponUncommonChance, WeaponRareChance;
        internal static ConfigEntry<bool> SpawnDebug;
        internal static ConfigEntry<bool> ForceEpicRolls, ForceMythicRolls;

        internal static Color EpicColor = new Color(0.9f, 0.15f, 0.15f);
        internal static Color MythicColor = new Color(0.63f, 0.13f, 0.94f);
        internal static Color VanillaRareColor;

        private void Awake()
        {
            Log = Logger;

            EpicColorHex = Config.Bind("Epic", "Color", "#E62626",
                "Epic tier color in HTML format (#RRGGBB).");
            EpicLabel = Config.Bind("Epic", "Label", "EPIC",
                "Epic tier label shown on entry cards.");
            EpicVehicleChance = Config.Bind("Epic", "VehicleUpgradeChance", 0.15f,
                new ConfigDescription("Chance to upgrade Rare->Epic on vehicle spawn.", new AcceptableValueRange<float>(0f, 1f)));
            EpicWeaponChance = Config.Bind("Epic", "WeaponUpgradeChance", 0.1f,
                new ConfigDescription("Chance to upgrade Rare->Epic when handing weapons to bots.", new AcceptableValueRange<float>(0f, 1f)));

            MythicColorHex = Config.Bind("Mythic", "Color", "#A020F0",
                "Mythic tier color in HTML format (#RRGGBB).");
            MythicLabel = Config.Bind("Mythic", "Label", "MYTHIC",
                "Mythic tier label shown on entry cards.");
            MythicVehicleChance = Config.Bind("Mythic", "VehicleUpgradeChance", 0.1f,
                new ConfigDescription("Chance to upgrade Epic->Mythic on vehicle spawn.", new AcceptableValueRange<float>(0f, 1f)));
            MythicWeaponChance = Config.Bind("Mythic", "WeaponUpgradeChance", 0.05f,
                new ConfigDescription("Chance to upgrade Epic->Mythic when handing weapons to bots.", new AcceptableValueRange<float>(0f, 1f)));

            VehicleUncommonChance = Config.Bind("SpawnChances", "VehicleUncommonChance", 0.3f,
                new ConfigDescription("Chance to upgrade Common->Uncommon on vehicle spawn. "
                    + "0.30 is the VANILLA value - yes, the base game secretly rolls this on every spawner "
                    + "that has rolling enabled; assigning vehicles to tiers just makes it visible. "
                    + "Lower it if you want colored vehicles to feel rare again.",
                    new AcceptableValueRange<float>(0f, 1f)));
            VehicleRareChance = Config.Bind("SpawnChances", "VehicleRareChance", 0.3333333f,
                new ConfigDescription("Chance to upgrade Uncommon->Rare on vehicle spawn (vanilla: 0.333).",
                    new AcceptableValueRange<float>(0f, 1f)));
            WeaponUncommonChance = Config.Bind("SpawnChances", "WeaponUncommonChance", 0.25f,
                new ConfigDescription("Chance to upgrade Common->Uncommon for bot weapons (vanilla: 0.25).",
                    new AcceptableValueRange<float>(0f, 1f)));
            WeaponRareChance = Config.Bind("SpawnChances", "WeaponRareChance", 0.2f,
                new ConfigDescription("Chance to upgrade Uncommon->Rare for bot weapons (vanilla: 0.20).",
                    new AcceptableValueRange<float>(0f, 1f)));

            SpawnDebug = Config.Bind("Debug", "SpawnDebug", false,
                "Log every rarity tier roll on spawn. Turn off after debugging.");
            ForceEpicRolls = Config.Bind("Debug", "ForceEpicRolls", false,
                "TEST: every spawn/hand-out tries the Epic tier first.");
            ForceMythicRolls = Config.Bind("Debug", "ForceMythicRolls", false,
                "TEST: every spawn/hand-out tries the Mythic tier first (overrides Epic).");

            if (ColorUtility.TryParseHtmlString(EpicColorHex.Value, out var pe)) EpicColor = pe;
            if (ColorUtility.TryParseHtmlString(MythicColorHex.Value, out var pm)) MythicColor = pm;

            // The tiers are injected into the enum by the PRELOADER PATCHER.
            // Here we only verify the result.
            if (Enum.GetValues(typeof(RarityTier)).Length != ExpectedTierCount)
            {
                Log.LogError($"[ExtendedRarity] Enum RarityTier was not extended to {ExpectedTierCount} values! " +
                    "Make sure ExtendedRarity.Patcher.dll is up to date in BepInEx/patchers. New tiers are not activated.");
                return;
            }

            // Force static constructors so our writes below are not
            // overwritten by their lazy initialization later.
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(RarityTierUtils).TypeHandle);
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(ColorScheme).TypeHandle);
            VanillaRareColor = ColorScheme.rarityColors[2];

            new Harmony("com.dolph.extendedrarity").PatchAll();

            // Colors: [3] = Epic, [4] = Mythic.
            var colors = new Color[ExpectedTierCount];
            Array.Copy(ColorScheme.rarityColors, colors, Math.Min(3, ColorScheme.rarityColors.Length));
            colors[3] = EpicColor;
            colors[4] = MythicColor;
            ColorScheme.rarityColors = colors;

            // "Roll a higher tier" chance tables: added Rare->Epic and
            // Epic->Mythic steps. RollTier walks the table while the index
            // is < table length, so extending the array is what makes the
            // new tiers reachable.
            RarityTierUtils.VEHICLE_ROLL_HIGHER_TIER_CHANCE_TABLE =
                new[] { VehicleUncommonChance.Value, VehicleRareChance.Value, EpicVehicleChance.Value, MythicVehicleChance.Value };
            RarityTierUtils.WEAPON_ROLL_HIGHER_TIER_CHANCE_TABLE =
                new[] { WeaponUncommonChance.Value, WeaponRareChance.Value, EpicWeaponChance.Value, MythicWeaponChance.Value };

            Log.LogInfo($"[ExtendedRarity] Tier system extended: {string.Join(", ", RarityTierUtils.AllTiers())}. " +
                $"Vehicle chain: {VehicleUncommonChance.Value:P0} -> {VehicleRareChance.Value:P0} -> {EpicVehicleChance.Value:P0} -> {MythicVehicleChance.Value:P0}. " +
                $"Weapon chain: {WeaponUncommonChance.Value:P0} -> {WeaponRareChance.Value:P0} -> {EpicWeaponChance.Value:P0} -> {MythicWeaponChance.Value:P0}.");
            Log.LogInfo("Extended Rarity loaded. Painting hotkeys: 5 = Epic, 6 = Mythic.");
        }

        internal static RarityTier? ForcedTier()
        {
            if (ForceMythicRolls.Value) return Mythic;
            if (ForceEpicRolls.Value) return Epic;
            return null;
        }

        internal static string LabelFor(string rawText)
        {
            switch (rawText)
            {
                case "3": case "Epic": return EpicLabel.Value;
                case "4": case "Mythic": return MythicLabel.Value;
                default: return null;
            }
        }
    }

    // ===== Entry card labels =====

    [HarmonyPatch(typeof(VehicleEntryObject), nameof(VehicleEntryObject.UpdateVisualRarity))]
    internal static class VehicleEntryLabel_Patch
    {
        private static void Postfix(VehicleEntryObject __instance)
        {
            if (__instance.rarityText == null) return;
            var label = Plugin.LabelFor(__instance.rarityText.text);
            if (label != null) __instance.rarityText.text = label;
        }
    }

    [HarmonyPatch(typeof(WeaponEntryObject), nameof(WeaponEntryObject.UpdateVisualRarity))]
    internal static class WeaponEntryLabel_Patch
    {
        private static void Postfix(WeaponEntryObject __instance)
        {
            if (__instance.rarityText == null) return;
            var label = Plugin.LabelFor(__instance.rarityText.text);
            if (label != null) __instance.rarityText.text = label;
        }
    }

    // ===== Painting panel circles + hotkeys 5/6 =====

    [HarmonyPatch(typeof(EntryPaintingTool), "Awake")]
    internal static class PaintingToolAwake_Patch
    {
        internal static Toggle EpicToggle, MythicToggle;

        private static void Postfix(EntryPaintingTool __instance)
        {
            try
            {
                // FieldInfo instead of AccessTools.FieldRefAccess: this Mono
                // build has no Reflection.Emit (SRE: False), where FieldRefAccess
                // fails silently.
                var togglesField = typeof(EntryPaintingTool).GetField("toggles",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var toggles = (Toggle[])togglesField.GetValue(__instance);
                if (toggles == null || toggles.Length < 4)
                {
                    Plugin.Log.LogWarning("[ExtendedRarity] Unexpected painting panel layout, circles not added.");
                    return;
                }
                foreach (var t in toggles)
                    if (t != null && t.gameObject.name.Contains("(ExtendedRarity)")) return; // already added

                Toggle rare = toggles[2];
                Toggle disable = toggles[3];
                var rtUncommon = (RectTransform)toggles[1].transform;
                var rtRare = (RectTransform)rare.transform;
                Vector2 step = rtRare.anchoredPosition - rtUncommon.anchoredPosition;
                bool manualLayout = rare.transform.parent.GetComponent<LayoutGroup>() == null;

                EpicToggle = BuildToggle(__instance, rare, disable, "Toggle Epic (ExtendedRarity)",
                    Plugin.EpicColor, (int)Plugin.Epic, manualLayout ? rtRare.anchoredPosition + step : (Vector2?)null);
                MythicToggle = BuildToggle(__instance, rare, disable, "Toggle Mythic (ExtendedRarity)",
                    Plugin.MythicColor, (int)Plugin.Mythic, manualLayout ? rtRare.anchoredPosition + step * 2 : (Vector2?)null);

                if (manualLayout)
                    ((RectTransform)disable.transform).anchoredPosition = rtRare.anchoredPosition + step * 3;

                // Append at the END of the array: the game's hotkey 4 hard-codes
                // toggles[3] as the Disable toggle, so ordering must be preserved.
                var extended = new Toggle[toggles.Length + 2];
                Array.Copy(toggles, extended, toggles.Length);
                extended[toggles.Length] = EpicToggle;
                extended[toggles.Length + 1] = MythicToggle;
                togglesField.SetValue(__instance, extended);

                Plugin.Log.LogInfo("[ExtendedRarity] Epic and Mythic circles added to the painting panel.");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"[ExtendedRarity] Failed to add circles: {e}");
            }
        }

        private static Toggle BuildToggle(EntryPaintingTool tool, Toggle template, Toggle before,
            string name, Color color, int rarityValue, Vector2? manualPos)
        {
            var clone = UnityEngine.Object.Instantiate(template.gameObject, template.transform.parent);
            clone.name = name;
            clone.transform.SetSiblingIndex(before.transform.GetSiblingIndex());

            var toggle = clone.GetComponent<Toggle>();
            toggle.group = template.group;
            toggle.SetIsOnWithoutNotify(false);
            // The clone inherits the template's scene-wired onValueChanged
            // (persistent listeners survive RemoveAllListeners), so replace
            // the whole event object.
            toggle.onValueChanged = new Toggle.ToggleEvent();
            toggle.onValueChanged.AddListener(on =>
            {
                if (on) tool.ChangeRarityToSet(rarityValue);
            });

            // Recolor everything that matches the template's (Rare) blue.
            foreach (var g in clone.GetComponentsInChildren<Graphic>(true))
                if (SameColor(g.color, Plugin.VanillaRareColor))
                    g.color = color;

            if (manualPos.HasValue)
                ((RectTransform)clone.transform).anchoredPosition = manualPos.Value;

            return toggle;
        }

        private static bool SameColor(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) < 0.02f && Mathf.Abs(a.g - b.g) < 0.02f && Mathf.Abs(a.b - b.b) < 0.02f;
    }

    [HarmonyPatch(typeof(EntryPaintingTool), "Update")]
    internal static class PaintingToolHotkey_Patch
    {
        private static void Postfix()
        {
            if (Input.GetKeyDown(KeyCode.Alpha5) && PaintingToolAwake_Patch.EpicToggle != null)
                PaintingToolAwake_Patch.EpicToggle.isOn = true;
            if (Input.GetKeyDown(KeyCode.Alpha6) && PaintingToolAwake_Patch.MythicToggle != null)
                PaintingToolAwake_Patch.MythicToggle.isOn = true;
        }
    }

    // ===== Bot weapons: force mode and diagnostics =====

    // Force mode for weapons: override the tier roll result when the roll
    // used the weapon chance table (reference comparison is a reliable
    // marker of the roll source).
    [HarmonyPatch(typeof(RarityTierUtils), nameof(RarityTierUtils.RollTier))]
    internal static class RollTierWeapon_Patch
    {
        private static void Postfix(float[] chanceTable, ref RarityTier __result)
        {
            var forced = Plugin.ForcedTier();
            if (forced.HasValue &&
                ReferenceEquals(chanceTable, RarityTierUtils.WEAPON_ROLL_HIGHER_TIER_CHANCE_TABLE))
                __result = forced.Value;
        }
    }

    [HarmonyPatch]
    internal static class AiWeaponDebug_Patch
    {
        [HarmonyPatch(typeof(WeaponManager), nameof(WeaponManager.GetAiWeaponPrimary))]
        [HarmonyPostfix]
        private static void Primary(int team, WeaponManager.WeaponEntry __result) => Report("primary", team, __result);

        [HarmonyPatch(typeof(WeaponManager), nameof(WeaponManager.GetAiWeaponSecondary))]
        [HarmonyPostfix]
        private static void Secondary(int team, WeaponManager.WeaponEntry __result) => Report("secondary", team, __result);

        private static void Report(string slot, int team, WeaponManager.WeaponEntry entry)
        {
            if (!Plugin.SpawnDebug.Value || entry == null) return;
            string tierInfo = "";
            var gm = GameManager.instance;
            if (gm != null && gm.gameInfo != null && team >= 0 && team < gm.gameInfo.team.Length)
            {
                var ti = gm.gameInfo.team[team];
                if (ti != null && ti.availableWeapons != null &&
                    ti.availableWeapons.IsAvailable(entry, out var tier))
                    tierInfo = $" (from {tier} pool)";
            }
            Plugin.Log.LogInfo($"[ExtendedRarity][weapon] team {team} bot, {slot}: {entry.name}{tierInfo}");
        }
    }

    // ===== Vehicle spawn: fallback cap removal, force mode, diagnostics =====

    [HarmonyPatch(typeof(VehicleSlotInfo), nameof(VehicleSlotInfo.RollVehicle))]
    internal static class RollVehicle_Patch
    {
        private static void Prefix(ref RarityTier tier, ref RarityTier highestFallbackTier)
        {
            // Compensate for the game's inlined HighestRarity=Rare constant:
            // without this, a vehicle painted with a new tier NEVER spawns
            // if it is the only vehicle in its slot.
            if (highestFallbackTier == RarityTier.Rare)
                highestFallbackTier = Plugin.TopTier;
            var forced = Plugin.ForcedTier();
            if (forced.HasValue)
                tier = forced.Value;
        }

        private static void Postfix(RarityTier tier, VehicleSlotInfo __instance, VehicleInfo __result)
        {
            if (!Plugin.SpawnDebug.Value) return;
            string from = "";
            if (__result != null && __instance.IsAvailable(__result, out var actualTier))
                from = $" (from {actualTier} pool)";
            Plugin.Log.LogInfo($"[ExtendedRarity][spawn] tier roll: {tier} -> " +
                (__result?.prefab != null ? __result.prefab.name : "EMPTY") + from);
        }
    }
}
