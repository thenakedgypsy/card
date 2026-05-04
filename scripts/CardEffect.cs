using Godot;
using System;
using System.Collections.Generic;

public partial class CardEffect : Node2D
{
    public enum EffectType
    {
        EnemyDamage,
        Summon,
        SummonModify,
        CoreModify,
        DeckModify      
    }

    public EffectType type;
    public Card.Element element;
    public Dictionary<string, Variant> effectData;
    public string cardID;

    public override void _Ready()
    {
    
    }

    public void ConstructEffect(Card.Element ele, Dictionary<string, Variant> data, string cardId)
    {
        element = ele;
        effectData = data;
        cardID = cardId;
        if (effectData.ContainsKey("effectType") &&
            Enum.TryParse(effectData["effectType"].ToString(), out EffectType parsedElement))
            type = parsedElement;
    }

    public void Trigger()
    {
        if (type == EffectType.Summon)
        {
            string health = effectData["health"].ToString();
            GD.Print($"Summoning a summon with {health} hp");
        }
        if (type == EffectType.EnemyDamage)
        {
            String damage = effectData["damage"].ToString();
            GD.Print($"BLAMMO YOU WOULD DO {damage} DAMAGE NOW");
        }
    }
}