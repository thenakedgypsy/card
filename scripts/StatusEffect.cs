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

    public Type TypeName;

    #pragma warning disable IDE0059 //this turns off annoying ide rule thats flagged here

    public void Setup(Godot.Collections.Dictionary<string, Variant> data, Card.Element ele)
    {
        Damage = int.Parse(data["damage"].ToString());
        Element = ele;
        TurnsLeftActive = data["turnsActive"].ToString().ToInt();
        StatusEffect.Type TypeName;
        
        if (data.ContainsKey("statusType") && Enum.TryParse(data["statusType"].ToString(), out StatusEffect.Type parsedStatusType))
        TypeName = parsedStatusType;
        InstantiateSprite();
    }

    public void InstantiateSprite()
    {
        Sprite2D sprite = GetNode<Sprite2D>("Sprite2D");
        Texture2D texture = GD.Load<Texture2D>($"res://assets/icons/{TypeName}.png");
        sprite.Texture = texture;
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

                    _triggerStun();
                    break;
                case StatusEffect.Type.Disarm:
                    break;
            }
        }
        else
        {
            _checkToRemoveStun();
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
    private void _triggerStun()
    {
        Enemy parent = GetParent<Node2D>() as Enemy;
        if (parent.HasMethod("TakeDamage"))
        {
            parent.TakeDamage(Damage, Element);
        }
   
        parent.MoveDistance = 0;

        GD.Print($"_triggerStun {Damage} damage. {TurnsLeftActive} turns left.");
    }

    private void _checkToRemoveStun()
    {
        Enemy parent = GetParent<Node2D>() as Enemy;
        if(TypeName == StatusEffect.Type.Stun)
        {
            //will need a variable outside of enemy to reset to OG value
             parent.MoveDistance = 4;
        }
    }
}