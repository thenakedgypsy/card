using Godot;
using System;

public partial class Effect : Node2D
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