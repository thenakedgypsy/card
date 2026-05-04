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

    public override void _Ready()
    {
    
    }

    public void ConstructEffect(Card.Element ele, Dictionary<string, Variant> data)
    {
        element = ele;
        effectData = data;
        if (effectData.ContainsKey("effectType") &&
            Enum.TryParse(effectData["effectType"].ToString(), out EffectType parsedElement))
            type = parsedElement;
    }

    public void Trigger()
    {
        if (type == EffectType.Summon)
        {
            
        }
        if (type == EffectType.EnemyDamage)
        {
            String damage = effectData["damage"].ToString();
            GD.Print($"BLAMMO YOU WOULD DO {damage} DAMAGE NOW");
        }
    }
}