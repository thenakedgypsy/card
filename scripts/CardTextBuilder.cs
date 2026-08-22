using Godot;
using Godot.Collections;
using System.Collections.Generic;

public static class CardTextBuilder
{
    private const string TermsPath = "res://assets/cards/text/en_gb/terms.json";
    private static Dictionary _terms;

    private static void LoadTerms()
    {
        if (_terms != null) return;

        if (!FileAccess.FileExists(TermsPath))
        {
            GD.PrintErr($"Localization terms missing at: {TermsPath}");
            _terms = new Dictionary();
            return;
        }

        using var file = FileAccess.Open(TermsPath, FileAccess.ModeFlags.Read);
        var json = new Json();
        if (json.Parse(file.GetAsText()) == Error.Ok)
        {
            _terms = json.Data.AsGodotDictionary();
        }
    }

    public static string BuildCardText(string cardID)
    {
        LoadTerms();

        string dataPath = $"res://assets/cards/data/{cardID}.json";

        var data = LoadJson(dataPath);

        if (data == null) return string.Empty;

        string cardName = data != null && data.ContainsKey("name") 
            ? data["name"].ToString() 
            : "Unit";

        string cardType = data.ContainsKey("type") ? data["type"].ToString() : "";

        if (cardType == "Summon" && data.ContainsKey("effectData"))
        {
            return BuildSummonText(cardName, data["effectData"].AsGodotDictionary());
        }
        else if (data.ContainsKey("effects"))
        {
            return BuildSpellText(data["effects"].AsGodotArray());
        }

        return string.Empty;
    }

    private static string BuildSummonText(string name, Dictionary effectData)
    {
        int health = effectData.ContainsKey("health") ? (int)effectData["health"] : 0;
        int range = effectData.ContainsKey("range") ? (int)effectData["range"] : 0;

        string baseText = GetTerm("summon_base")
            .Replace("{name}", name)
            .Replace("{health}", health.ToString())
            .Replace("{range}", range.ToString());

        if (effectData.ContainsKey("effects") && effectData["effects"].VariantType == Variant.Type.Array)
        {
            var subEffects = effectData["effects"].AsGodotArray();
            if (subEffects.Count > 0)
            {
                string combinedEffects = BuildSpellText(subEffects);
                string suffix = GetTerm("summon_and_then")
                    .Replace("{name}", name)
                    .Replace("{effects}", combinedEffects);

                return baseText + suffix;
            }
        }

        return baseText;
    }

    private static string BuildSpellText(Array effectsArray)
    {
        List<string> effectStrings = new List<string>();

        for (int i = 0; i < effectsArray.Count; i++)
        {
            var effect = effectsArray[i].AsGodotDictionary();
            // Use lowercase variants for any effect after the first (index > 0)
            bool isLower = i > 0;
            string effectText = ParseSingleEffect(effect, isLower);
            if (!string.IsNullOrEmpty(effectText))
            {
                effectStrings.Add(effectText);
            }
        }

        string joiner = GetTerm("and");
        return string.Join(joiner, effectStrings);
    }

    private static string ParseSingleEffect(Dictionary effect, bool isLower = false)
    {
        string type = effect.ContainsKey("effectType") ? effect["effectType"].ToString() : "";

        switch (type)
        {
            case "EnemyDamage":
                int damage = effect.ContainsKey("damage") ? (int)effect["damage"] : 0;
                string key = isLower ? "enemy_damage_lower" : "enemy_damage";
                string baseDamage = GetTerm(key).Replace("{damage}", damage.ToString());

                if (effect.ContainsKey("splashTiles") && (int)effect["splashTiles"] > 0)
                {
                    string splash = GetTerm("splash_area").Replace("{splashTiles}", effect["splashTiles"].ToString());
                    baseDamage += splash;
                }
                return baseDamage;

            case "StatusEffect":
                string status = effect.ContainsKey("statusType") ? effect["statusType"].ToString() : "";
                int turns = effect.ContainsKey("turnsActive") ? (int)effect["turnsActive"] : 1;
                int statusDmg = effect.ContainsKey("damage") ? (int)effect["damage"] : 0;

                string templateKey;
                if (status == "Burn")
                {
                    templateKey = turns > 1 
                        ? (isLower ? "burn_status_plural_lower" : "burn_status_plural")
                        : (isLower ? "burn_status_lower" : "burn_status");
                }
                else
                {
                    templateKey = turns > 1 
                        ? (isLower ? "status_effect_plural_lower" : "status_effect_plural")
                        : (isLower ? "status_effect_lower" : "status_effect");
                }

                string statusStr = GetTerm(templateKey)
                    .Replace("{statusType}", status)
                    .Replace("{damage}", statusDmg.ToString())
                    .Replace("{turnsActive}", turns.ToString());

                if (effect.ContainsKey("splashTiles") && (int)effect["splashTiles"] > 0)
                {
                    statusStr += GetTerm("splash_area").Replace("{splashTiles}", effect["splashTiles"].ToString());
                }

                return statusStr;

            default:
                return string.Empty;
        }
    }

    private static string GetTerm(string key)
    {
        return _terms.ContainsKey(key) ? _terms[key].ToString() : key;
    }

    private static Dictionary LoadJson(string path)
    {
        if (!FileAccess.FileExists(path)) return null;
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        var json = new Json();
        return json.Parse(file.GetAsText()) == Error.Ok ? json.Data.AsGodotDictionary() : null;
    }
}