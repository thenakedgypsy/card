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

    public void ApplyStatusEffect()
    {

        GD.Print($"Apply Status!!!! before switch");
        switch (TypeName)
        {
            case StatusEffect.Type.Burn:
                GD.Print($"In status. Turns active: {TurnsLeftActive}");

                if (TurnsLeftActive > 0)
                {
                    GD.Print("In TurnsLeftActive");
                    //could seperate this out into a seperate Apply StatusEffect function
                    //as I've named this function Check - and it also applies the effect mechanic 
                    ApplyBurn();
                }
                else
                {
                    QueueFree();
                }
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

    //generic apply status 
    //dont think we'll need this for burn at least - just does a damage per turn.
    public void ApplyBurn()
    {
        Enemy parent = GetParent<Node2D>() as Enemy;
        TurnsLeftActive--;
        if (parent.HasMethod("TakeDamage"))
        {
            parent.TakeDamage(Damage, Card.Element.Fire);
        }
     
        GD.Print($"ApplyBurn {Damage} damage. {TurnsLeftActive} turns left.");
    }
}