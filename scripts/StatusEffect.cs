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
                //not sure what to do with slow - same as stun for now.
                    break;
                case StatusEffect.Type.Confuse:
                    break;
                case StatusEffect.Type.Haste:
                    break;
                case StatusEffect.Type.Stun:
                        GD.Print($"trigger stun!!!! ");

                    _triggerStun();
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
        Node2D parent = GetParent<Node2D>();
        if (parent.HasMethod("TakeDamage"))
        {
            parent.Call("TakeDamage", Damage);
        }
     
        GD.Print($"ApplyBurn {Damage} damage. {TurnsLeftActive} turns left.");
    }
    private void _triggerStun()
    {
        Enemy parent = GetParent<Node2D>() as Enemy;
        if (parent.HasMethod("TakeDamage"))
        {
            parent.TakeDamage(Damage);
        }
     
        GD.Print($"_triggerStun {Damage} damage. {TurnsLeftActive} turns left.");
    }
}