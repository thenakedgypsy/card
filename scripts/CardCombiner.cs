using Godot;

using System;
using System.Collections.Generic;

public static class CardCombiner
{
    // Holds dynamically created card data in memory during runtime
    private static readonly Dictionary<string, Godot.Collections.Dictionary<string, Variant>> DynamicCards = new();

    // Map generated combination IDs back to the underlying atomic/base card IDs that form them
    private static readonly Dictionary<string, List<string>> CombinationRecipes = new();

    /// <summary>
    /// Combines any two card IDs (base or already combined) into a new combined Card ID.
    /// Supports infinite recursive combinations.
    /// </summary>
    public static string CombineCards(string cardIdA, string cardIdB)
    {
        // 1. Fetch data for both cards to compare their elements
        var dataA = GetCardData(cardIdA);
        var dataB = GetCardData(cardIdB);
    
        if (dataA == null || dataB == null)
        {
            GD.PrintErr($"CardCombiner: Failed to retrieve data for {cardIdA} or {cardIdB}");
            return null;
        }
    
        // 2. Validate that both cards share the exact same element
        string elementA = dataA.GetValueOrDefault("element", "Neutral").ToString();
        string elementB = dataB.GetValueOrDefault("element", "Neutral").ToString();
    
        if (!string.Equals(elementA, elementB, StringComparison.OrdinalIgnoreCase))
        {
            GD.Print($"CardCombiner: Combination failed. '{cardIdA}' ({elementA}) and '{cardIdB}' ({elementB}) are different elements.");
            return null; // Prevents combination
        }
    
        // 3. Unravel both card IDs into their base constituent card IDs
        List<string> baseCardsA = GetBaseCardIds(cardIdA);
        List<string> baseCardsB = GetBaseCardIds(cardIdB);
    
        List<string> allBaseCards = new List<string>();
        allBaseCards.AddRange(baseCardsA);
        allBaseCards.AddRange(baseCardsB);
    
        // Sort so that (Fireball + Stun) produces the EXACT same ID as (Stun + Fireball)
        allBaseCards.Sort(StringComparer.Ordinal);
    
        // 4. Generate a clean, fixed-length ID based on constituents
        string combinationId = GenerateCombinationId(allBaseCards);
    
        // Save recipe mapping
        CombinationRecipes[combinationId] = allBaseCards;
    
        // If we've already generated this exact combo before, reuse it!
        if (DynamicCards.ContainsKey(combinationId))
        {
            return combinationId;
        }
    
        // 5. Load full data for every base card and construct merged definition
        List<Godot.Collections.Dictionary<string, Variant>> baseDataList = new List<Godot.Collections.Dictionary<string, Variant>>();
        foreach (string id in allBaseCards)
        {
            var data = LoadCardDataFromDisk(id);
            if (data != null)
            {
                baseDataList.Add(data);
            }
            else
            {
                GD.PrintErr($"CardCombiner: Failed to load base card data for ID: {id}");
            }
        }
    
        if (baseDataList.Count == 0) return null;
    
        // 6. Perform full merge across all base cards
        Godot.Collections.Dictionary<string, Variant> combinedData = MergeAllCardData(baseDataList);


        // Find the art of the rarest card
        string bestArtId = allBaseCards[0];
        string highestRaritySeen = "Common";

        for (int i = 0; i < allBaseCards.Count; i++)
        {
            string r = baseDataList[i].GetValueOrDefault("rarity", "Common").ToString();
            string newRarity = GetHigherRarity(highestRaritySeen, r);

            // If the new rarity is strictly higher, update our target art
            if (newRarity != highestRaritySeen)
            {
                highestRaritySeen = newRarity;
                bestArtId = allBaseCards[i];
            }
        }

        // Assign the most rare card's art
        combinedData["artId"] = bestArtId;;

        // Cache in memory
        DynamicCards[combinationId] = combinedData;

        return combinationId;
    }

    /// <summary>
    /// Returns card data dictionary (checks runtime combined cache first, falls back to JSON files).
    /// </summary>
    public static Godot.Collections.Dictionary<string, Variant> GetCardData(string cardId)
    {
        if (DynamicCards.TryGetValue(cardId, out var dynamicData))
        {
            return dynamicData;
        }

        return LoadCardDataFromDisk(cardId);
    }

    // ==========================================
    // UNRAVELING & ID GENERATION
    // ==========================================

    private static List<string> GetBaseCardIds(string cardId)
    {
        if (CombinationRecipes.TryGetValue(cardId, out var constituents))
        {
            return new List<string>(constituents);
        }
        
        // It's a standard base card ID from disk
        return new List<string> { cardId };
    }

    private static string GenerateCombinationId(List<string> sortedBaseCards)
    {
        string rawKey = string.Join("+", sortedBaseCards);
        // Create a short 32-bit hash representation to keep string IDs short & clean
        uint hash = 2166136261;
        foreach (char c in rawKey)
        {
            hash = (hash ^ c) * 16777619;
        }

        return $"comb_{hash:x8}";
    }

    // ==========================================
    // MERGING LOGIC
    // ==========================================

    private static Godot.Collections.Dictionary<string, Variant> MergeAllCardData(List<Godot.Collections.Dictionary<string, Variant>> cards)
    {
        var result = new Godot.Collections.Dictionary<string, Variant>();

        List<string> names = new List<string>();
        int totalCost = 0;

        // --- Summon Tracking Variables ---
        int totalHealth = 0;
        bool isSummon = false;
        int maxRange = 0;
        bool attacksEnemies = false;
        // ---------------------------------

        string highestRarity = "Common";
        string primaryType = cards[0].GetValueOrDefault("type", "Spell").ToString();
        string primaryElement = cards[0].GetValueOrDefault("element", "Neutral").ToString();

        List<Godot.Collections.Dictionary<string, Variant>> mergedEffects = new List<Godot.Collections.Dictionary<string, Variant>>();

        foreach (var card in cards)
        {
            string rarestName = cards[0].GetValueOrDefault("name", "Unknown").ToString();

            if (card.ContainsKey("name")) names.Add(card["name"].ToString());

            // Rarity upgrade check
            string r = card.GetValueOrDefault("rarity", "Common").ToString();
            string checkRarity = GetHigherRarity(highestRarity, r);
            if (checkRarity != highestRarity)
            {
                highestRarity = checkRarity;
                if (card.ContainsKey("name")) rarestName = card["name"].ToString();
            }

            result["name"] = GenerateEscalatingTitle(names.Count, rarestName);

            // Cost summation
            if (card.ContainsKey("cost")) totalCost += card["cost"].AsInt32();

            // --- NEW: Check for Summon Types and preserve specific Summon properties ---
            if (card.GetValueOrDefault("type", "").ToString() == "Summon")
            {
                isSummon = true;
            }

            if (card.ContainsKey("health"))
            {
                isSummon = true; // Fallback check just in case
                totalHealth += card["health"].AsInt32();
            }

            if (card.ContainsKey("range"))
            {
                int rValue = card["range"].AsInt32();
                if (rValue > maxRange) maxRange = rValue;
            }

            if (card.ContainsKey("attacksEnemies") && card["attacksEnemies"].AsBool())
            {
                attacksEnemies = true;
            }
            // --------------------------------------------------------------------------

            // Merge Effects (This will safely stack the spell effects into the summon's effect list)
            List<Godot.Collections.Dictionary<string, Variant>> cardEffects = ExtractEffectsList(card);
            foreach (var effect in cardEffects)
            {
                MergeSingleEffectIntoList(mergedEffects, effect);
            }
        }

        if (highestRarity != "Legendary")
        {
            switch (highestRarity)
            {
                case "Common":
                    highestRarity = "Uncommon";
                    break;
                case "Uncommon":
                    highestRarity = "Rare";
                    break;
                case "Rare":
                    highestRarity = "Epic";
                    break;
                case "Epic":
                    highestRarity = "Legendary";
                    break;
            }
        }

        // Apply calculated root properties
        result["cost"] = totalCost;
        result["element"] = primaryElement;
        result["rarity"] = highestRarity;

        // --- NEW: Force the type to Summon if any constituent card was a Summon ---
        result["type"] = isSummon ? "Summon" : primaryType;

        if (isSummon)
        {
            result["health"] = totalHealth;
            result["effectType"] = "Summon";

            // Apply the preserved properties so the Summon spawner doesn't break
            if (maxRange > 0) result["range"] = maxRange;
            result["attacksEnemies"] = attacksEnemies;
        }

        // Output Array of effects for Godot JSON compliance
        var godotEffectsArray = new Godot.Collections.Array<Godot.Collections.Dictionary<string, Variant>>();
        foreach (var eff in mergedEffects)
        {
            godotEffectsArray.Add(eff);
        }
        result["effects"] = godotEffectsArray;

        return result;
    }

    private static string GenerateEscalatingTitle(int comboCount, string coreName)
    {
        Random rng = new Random();
        int rand = rng.Next(3);
        if (rand == 0)
        {
            return comboCount switch
            {
                1 => coreName,
                2 => $"Fusion Enhanced {coreName}",
                3 => $"Greater {coreName} Cluster",
                4 => $"Pulsing {coreName} Integration",
                _ => $"Ultra-{coreName} Singularity"
            };
        }
        else if (rand == 1)
        {
            return comboCount switch
            {
                1 => coreName,
                2 => $"{coreName} Cluster",
                3 => $"Greater {coreName} Amalgam",
                4 => $"Ultra Hyrbidised {coreName}",
                _ => $"Omni-{coreName} Cataclysm"
            };
        }
        else
        {
            return comboCount switch
            {
                1 => coreName,
                2 => $"Hybridised {coreName}",
                3 => $"Twisted {coreName} Link",
                4 => $"Unstable {coreName} Anomaly",
                _ => $"{coreName} Final Form"
            };
        }
    }

    private static void MergeSingleEffectIntoList(
        List<Godot.Collections.Dictionary<string, Variant>> targetList, 
        Godot.Collections.Dictionary<string, Variant> newEffect)
    {
        string type = newEffect.GetValueOrDefault("effectType", "").ToString();
        string status = newEffect.GetValueOrDefault("statusType", "").ToString();

        // Match based on both effectType and statusType (e.g. StatusEffect + Burn)
        var existing = targetList.Find(e => 
            e.GetValueOrDefault("effectType", "").ToString() == type &&
            e.GetValueOrDefault("statusType", "").ToString() == status);

        if (existing != null)
        {
            // Sum numerical values for overlapping effects
            CombineKeys(existing, newEffect, "damage");
            CombineKeys(existing, newEffect, "turnsActive");
            CombineKeys(existing, newEffect, "splashTiles");
            CombineKeys(existing, newEffect, "range");
        }
        else
        {
            // Add as a distinct new effect entry
            targetList.Add(DuplicateDictionary(newEffect));
        }
    }

    private static void CombineKeys(Godot.Collections.Dictionary<string, Variant> target, Godot.Collections.Dictionary<string, Variant> source, string key)
    {
        if (source.ContainsKey(key))
        {
            int valTarget = target.ContainsKey(key) ? target[key].AsInt32() : 0;
            int valSource = source[key].AsInt32();
            target[key] = valTarget + valSource;
        }
    }

    // ==========================================
    // HELPERS & FILE I/O
    // ==========================================

    private static Godot.Collections.Dictionary<string, Variant> LoadCardDataFromDisk(string cardId)
    {
        string path = $"res://assets/cards/data/{cardId}.json";
        if (!FileAccess.FileExists(path)) return null;

        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        var json = new Json();
        if (json.Parse(file.GetAsText()) == Error.Ok)
        {
            return json.Data.AsGodotDictionary<string, Variant>();
        }
        return null;
    }

    private static List<Godot.Collections.Dictionary<string, Variant>> ExtractEffectsList(Godot.Collections.Dictionary<string, Variant> cardData)
    {
        var list = new List<Godot.Collections.Dictionary<string, Variant>>();
        if (cardData.ContainsKey("effects") && cardData["effects"].VariantType == Variant.Type.Array)
        {
            foreach (var item in cardData["effects"].AsGodotArray())
            {
                list.Add(item.AsGodotDictionary<string, Variant>());
            }
        }
        return list;
    }

    private static Godot.Collections.Dictionary<string, Variant> DuplicateDictionary(Godot.Collections.Dictionary<string, Variant> original)
    {
        var clone = new Godot.Collections.Dictionary<string, Variant>();
        foreach (var kvp in original) clone[kvp.Key] = kvp.Value;
        return clone;
    }

    private static string GetHigherRarity(string rarityA, string rarityB)
    {
        List<string> rarities = new() { "Common", "Uncommon", "Rare", "Epic", "Legendary" };
        int idxA = rarities.IndexOf(rarityA);
        int idxB = rarities.IndexOf(rarityB);
        return rarities[Math.Max(idxA >= 0 ? idxA : 0, idxB >= 0 ? idxB : 0)];
    }
}