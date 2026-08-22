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

    private int Power;

    public int TurnsLeftActive;

    public Type TypeName;

    #pragma warning disable IDE0059 //this turns off annoying ide rule thats flagged here
    private Enemy _getEnemyTarget() => GetParent<Node2D>() as Enemy;

    public void Setup(Godot.Collections.Dictionary<string, Variant> data, Card.Element ele)
    {
        
        Element = ele;
        TurnsLeftActive = data["turnsActive"].ToString().ToInt();
        Power = data["power"].ToString().ToInt();

        if (data.ContainsKey("damage"))
        {
            Damage = data["damage"].ToString().ToInt();
        }
        
        if (data.ContainsKey("statusType") && Enum.TryParse(data["statusType"].ToString(), out StatusEffect.Type parsedStatusType))
        TypeName = parsedStatusType;
        GD.Print("Status setup", data);
        InstantiateSprite();

        GD.Print($"power = {Power} Turns left {TurnsLeftActive} status type {TypeName}");
    }

    public void InstantiateSprite()
    {
        Sprite2D sprite = GetNode<Sprite2D>("Sprite2D");
        Texture2D texture = GD.Load<Texture2D>($"res://assets/icons/{TypeName}.png");
        sprite.Texture = texture;
    }

    //handle anything that is needed when this is applied to the enemie
    public void OnApplied()
    {
        switch (TypeName)
        {
            case Type.Stun:
                _triggerStun();
                break;
        }
    }

    public void TriggerStatusEffect()
    {
        GD.Print("Triggering status");
        if (TurnsLeftActive > 0 )
        {
            TurnsLeftActive--;
            switch (TypeName)
            {
                case StatusEffect.Type.Burn:
                    _triggerBurn();
                    break;
                case StatusEffect.Type.Slow:
                    _triggerSlow();
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
            if(Damage > 0) _enemyTakeDamage();
        }
        else
        {
            QueueFree(); 
        }
    }

    private void _triggerBurn()
    {
        //leaving this in for now incase other mechanics happen
    }
    private void _triggerSlow()
    {
        int enemyMoveDistance = _getEnemyTarget().MoveDistance; 
        int slowAmount = enemyMoveDistance - Power;
        _getEnemyTarget().IsSlowed = true;
        _getEnemyTarget().MoveDistance = slowAmount;
        _getEnemyTarget().Set("RemainingMovement", slowAmount);

        GD.Print($"_triggerSlow {TurnsLeftActive} turns left. Move speed {slowAmount}");

    }
    private void _triggerStun()
    {
        //removed the damage from here. can be applied via a seperate effect. 
        //using a bool rather than modulating.
        _getEnemyTarget().IsStunned = true;
        _getEnemyTarget().MoveDistance = 0;
        _getEnemyTarget().Set("RemainingMovement", 0);


        GD.Print($"_triggerStun {TurnsLeftActive} turns left.");
    }

    private void _enemyTakeDamage()
    {
        if (_getEnemyTarget().HasMethod("TakeDamage"))
        {
            _getEnemyTarget().TakeDamage(Damage, Element);
        }
    }
}