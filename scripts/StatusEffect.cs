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

        switch (TypeName)
        {
            case StatusEffect.Type.Burn:

                if (TurnsLeftActive > 1 )
                {
                    GD.Print("In TriggerStatusEffect then if TurnsLeftActive");
                    //could seperate this out into a seperate Apply StatusEffect function
                    //as I've named this function Check - and it also applies the effect mechanic 
                    GD.Print($"status type name: {TypeName} Turns left{TurnsLeftActive}");
                    TriggerBurn();
                    TurnsLeftActive--;
                }
                else if(TurnsLeftActive == 1)
                {
                    TriggerBurn();
                    GD.Print($"Removing Burn!");
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
    public void TriggerBurn()
    {
        Node2D parent = GetParent<Node2D>();
        if (parent.HasMethod("TakeDamage"))
        {
            parent.Call("TakeDamage", Damage);
        }
     
        GD.Print($"ApplyBurn {Damage} damage. {TurnsLeftActive} turns left.");
    }
}