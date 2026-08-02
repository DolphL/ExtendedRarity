using System.Collections.Generic;
using Mono.Cecil;

namespace ExtendedRarity
{
    // BepInEx preloader patcher: runs BEFORE Assembly-CSharp is loaded
    // and injects new values directly into the RarityTier enum.
    // After this, Enum.GetValues returns the extended tier list, and all
    // game logic (pools, fallbacks, serialization, labels) treats the
    // new tiers as native.
    public static class Patcher
    {
        public static IEnumerable<string> TargetDLLs { get; } = new[] { "Assembly-CSharp.dll" };

        public static void Patch(AssemblyDefinition assembly)
        {
            var rarityTier = assembly.MainModule.GetType("RarityTier");
            if (rarityTier == null)
            {
                System.Console.WriteLine("[ExtendedRarity.Patcher] enum RarityTier not found - patch skipped.");
                return;
            }

            // New tiers: name -> numeric value.
            var newTiers = new (string name, int value)[] { ("Epic", 3), ("Mythic", 4) };

            foreach (var (name, value) in newTiers)
            {
                bool exists = false;
                foreach (var f in rarityTier.Fields)
                    if (f.Name == name) { exists = true; break; }
                if (exists) continue;

                var field = new FieldDefinition(
                    name,
                    FieldAttributes.Public | FieldAttributes.Static |
                    FieldAttributes.Literal | FieldAttributes.HasDefault,
                    rarityTier)
                {
                    Constant = value
                };
                rarityTier.Fields.Add(field);
                System.Console.WriteLine($"[ExtendedRarity.Patcher] Added value {name} = {value} to enum RarityTier.");
            }
        }
    }
}
