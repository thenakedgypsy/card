using Godot;
using System;

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
}