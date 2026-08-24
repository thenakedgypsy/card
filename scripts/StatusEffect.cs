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
        Stun,
        Disarm,
        Haste, //for wizards to buff enemeies
        Shield ////for wizards to protect enemeies
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
        if (data.ContainsKey("power")) Power = data["power"].ToString().ToInt();

        if (data.ContainsKey("damage"))
        {
            Damage = data["damage"].ToString().ToInt();
        }
        
        if (data.ContainsKey("statusType") && Enum.TryParse(data["statusType"].ToString(), out StatusEffect.Type parsedStatusType))
        TypeName = parsedStatusType;
        //GD.Print("Status setup", data);
        InstantiateSprite();

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
                    break;
                case StatusEffect.Type.Slow:
                    if (_canApplySlow()) _triggerSlow();
                    break;
                case StatusEffect.Type.Confuse:
                    _triggerConfuse();
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
            GD.Print("QUEUE FREE");
            QueueFree(); 
        }
    }

    private void _triggerConfuse()
    {
        if (!_getEnemyTarget().IsConfused) _getEnemyTarget().IsConfused = true;
    }
    private void _triggerSlow()
    {
        int enemyMoveDistance = _getEnemyTarget().MoveDistance; 
        int slowAmount = enemyMoveDistance - Power;
        _getEnemyTarget().IsSlowed = true;
        _getEnemyTarget().MoveDistance = slowAmount;
        _getEnemyTarget().Set("RemainingMovement", slowAmount);
    }

    private bool _canApplySlow()
    {
        int curEnemyMoveDistance = _getEnemyTarget().MoveDistance; 
        int defaultEnemyMoveDistance = _getEnemyTarget().DefaultMoveDistance;

        if (curEnemyMoveDistance < defaultEnemyMoveDistance)
        {
            return false;
        }

        return true;
    }
    private void _triggerStun()
    {
        _getEnemyTarget().IsStunned = true;
        _getEnemyTarget().MoveDistance = 0;
        _getEnemyTarget().Set("RemainingMovement", 0);
    }

    private void _enemyTakeDamage()
    {
        if (_getEnemyTarget().HasMethod("TakeDamage"))
        {
            _getEnemyTarget().TakeDamage(Damage, Element);
        }
    }

    public void RemoveStatus() => QueueFree();

}