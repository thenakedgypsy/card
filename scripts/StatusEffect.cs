using Godot;
using System;
using System.Collections.Generic;

public partial class StatusEffect : Node2D
{    
    public enum Type
    {
        Burn,
        Slow,
        Confuse,
        Haste,
        Stun,
        Disarm
    }

	public Card.Element Element;
	public int Damage;

    public int TurnsLeftActive;

    public StatusEffect.Type TypeName;

    public void Setup(Dictionary<string, Variant> data, Card.Element ele)
    {
        Damage = int.Parse(data["damage"].ToString());
        Element = ele;
        TurnsLeftActive = data["turnsActive"].ToString().ToInt();
        StatusEffect.Type TypeName;
        if (data.ContainsKey("statusType") && Enum.TryParse(data["statusType"].ToString(), out StatusEffect.Type parsedStatusType))
        TypeName = parsedStatusType;
    }

    public void TriggerStatusEffect()
    {
        if (TurnsLeftActive > 0 )
        {
            TurnsLeftActive--;
            switch (TypeName)
            {
                case StatusEffect.Type.Burn:
                    TriggerBurn();
                    break;
                case StatusEffect.Type.Slow:
                    break;
                case StatusEffect.Type.Confuse:
                    break;
                case StatusEffect.Type.Haste:
                    break;
                case StatusEffect.Type.Stun:
                    break;
                case StatusEffect.Type.Disarm:
                    break;
            }
        }
        else
        {
            QueueFree(); 
        }
    }

    public void TriggerBurn()
    {
        Enemy parent = GetParent<Node2D>() as Enemy;
        if (parent.HasMethod("TakeDamage"))
        {
            parent.TakeDamage(Damage, Card.Element.Fire);
        }
     
        GD.Print($"ApplyBurn {Damage} damage. {TurnsLeftActive} turns left.");
    }
}