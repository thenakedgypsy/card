using Godot;
using System;
using System.Collections.Generic;

public partial class CardEffect : Node2D
{
    public enum EffectType
    {
        EnemyDamage,
        StatusEffect,
        Summon,
        SummonModify, //can include chance based modification
        CoreModify,
        DeckModify,
    }

    public Card.Element element;
    public string cardID;
    
    private Godot.Collections.Dictionary<string, Variant> _singleEffectData;
    private List<Godot.Collections.Dictionary<string, Variant>> _effectsList = new();
    private bool _isSummon = false;

    // Called for Summons or single-object effects
    public void ConstructEffect(Card.Element ele, Godot.Collections.Dictionary<string, Variant> data, string cardId)
    {
        element = ele;
        _singleEffectData = data;
        cardID = cardId;
        
        if (data.ContainsKey("effectType") && data["effectType"].AsString() == "Summon")
        {
            _isSummon = true;
        }
    }

    // Called for multi-step Spell cards
    public void ConstructSpellEffects(Card.Element ele, List<Godot.Collections.Dictionary<string, Variant>> effects, string cardId)
    {
        element = ele;
        _effectsList = effects;
        cardID = cardId;
        _isSummon = false;
    }

    public void Trigger()
    {
        if (_isSummon)
        {
            _Summon();
            QueueFree();
        }
        else
        {
            _TargetEnemy();
            QueueFree();
        }
    }

    private void _Summon()
    {
        string health = _singleEffectData["health"].AsString();
        PackedScene scene = GD.Load<PackedScene>("res://prefabs/SummonSpawner.tscn");
        SummonSpawner spawner = scene.Instantiate() as SummonSpawner;
        Mouse mouse = GetTree().GetFirstNodeInGroup("Mouse") as Mouse;           
        mouse.AddChild(spawner);
        
        spawner.Setup(element, _singleEffectData, cardID);
        GD.Print($"Summoning a summon with {health} hp");
    }

    private void _TargetSummon()
    {
        //stub - should target a summon using a spell targeter? 
        //Can we refactor TargetEnemy to just be target and the targeter itself can handle this logic? 
    }

    private void _TargetEnemy()
    {
        PackedScene scene = GD.Load<PackedScene>("res://prefabs/SpellTargeter.tscn");
        if (scene == null) return;

        SpellTargeter targeter = scene.Instantiate() as SpellTargeter;
        Mouse mouse = GetTree().GetFirstNodeInGroup("Mouse") as Mouse;
        mouse?.AddChild(targeter);

        // Pass the full array of spell effects to targeter
        targeter.Setup(element, _effectsList, cardID);
    }
}