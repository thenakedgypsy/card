using Godot;
using System;
using System.Collections.Generic;

public partial class SpellTargeter : Node2D
{
    private Sprite2D _sprite;

    private Dictionary<string, Variant> _data;
    private string _cardID;
    private Card.Element _element;

    private bool _readyToTarget;
    private Mouse _mouse;


    public override void _Ready()
    {
        _readyToTarget = true;
        _mouse = GetTree().GetFirstNodeInGroup("Mouse") as Mouse;
    }


    public override void _Process(double delta)
    {
        if (_readyToTarget)
        {
            GlobalPosition = GetGlobalMousePosition();
            CheckInput();
        }   
    }


    private void CheckInput()
    {
        if(Input.IsActionJustPressed("lClick"))
        {
            Enemy target = CheckTarget();
    
            if(target != null)
            {
                Cast(target);
            }
            else
            {
                FlashRed();
                GD.Print("Invalid target");
            }
        }
    }


    public void Setup(Card.Element ele, Dictionary<string, Variant> data, string cardID)
    {
        _sprite = GetNode<Sprite2D>("Sprite2D");

        string path = "res://assets/interface/Target.png";
        Texture2D texture = GD.Load<Texture2D>(path);

        _sprite.Texture = texture;
        _sprite.SelfModulate = new Color(1f, 1f, 1f, 0.5f);

        _data = data;
        _cardID = cardID;
        _element = ele;

        ZIndex = 3;
        _readyToTarget = true;
    }


    private Enemy CheckTarget()
    {
        return _mouse.GetHoveredEnemy();
    }

//add statuseffect in here?
    private void Cast(Enemy target)
    {
        int damage = int.Parse(_data["damage"].ToString());

        CardEffect.EffectType effectType = CardEffect.EffectType.EnemyDamage;

        if (_data.ContainsKey("effectType") &&
        Enum.TryParse(_data["effectType"].ToString(), out CardEffect.EffectType parsedEffectType))
        effectType = parsedEffectType;

        StatusEffect.Type statusType = StatusEffect.Type.Burn;

        if (_data.ContainsKey("statusType") &&
        Enum.TryParse(_data["statusTypet"].ToString(), out StatusEffect.Type parsedStatusType))
        statusType = parsedStatusType;

        GD.Print($"Casting {_cardID} on {target.Name} for {damage} damage");

        //how do i get the status effect?
        //
        if(effectType == CardEffect.EffectType.StatusEffect)
        {
            StatusEffect effect = new StatusEffect();
            effect.AddStatus(_element, _data);
  
            target.AddChild(effect);
            //dont think we need this in here:
            //probs best in the enemy
            //all info is in the status

            //to copy into enemy
            // switch (statusType)
            // {
            //     case StatusEffect.Type.Burn:
            //     //target add child status effect
              
            //         break;
            //     case StatusEffect.Type.Slow:

            //         break;
            //     case StatusEffect.Type.Confuse:

            //         break;
            //     case StatusEffect.Type.Haste:

            //         break;
            //     case StatusEffect.Type.Stun:

            //         break;

            //     case StatusEffect.Type.Disarm:

            //         break;
            // }
        }
        else
        {
            target.TakeDamage(damage);

            _readyToTarget = false;

        }

        QueueFree();

    }


    private async void FlashRed()
    {
        if (_sprite == null)
            return;


        Color original = _sprite.SelfModulate;

        Tween tween = CreateTween();

        tween.TweenProperty(
            _sprite,
            "self_modulate",
            Colors.Red,
            0.1f
        );

        tween.TweenProperty(
            _sprite,
            "self_modulate",
            original,
            0.1f
        );

        await ToSignal(tween, Tween.SignalName.Finished);
    }
}