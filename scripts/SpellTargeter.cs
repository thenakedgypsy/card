using Godot;
using System;
using System.Collections.Generic;

public partial class SpellTargeter : Node2D
{
    private Sprite2D _sprite;

    private CardEffect.EffectType _effectType;
    private Dictionary<string, Variant> _data;
    private string _cardID;
    private Card.Element _element;

    private bool _readyToTarget;
    private Mouse _mouse;
    private TurnManager _turnManager;

    private int _splashTiles = 0;
    private HashSet<Enemy> _highlightedEnemies = new HashSet<Enemy>();
    private Enemy _lastHoveredTarget;

    public override void _Ready()
    {
        _readyToTarget = true;
        _mouse = GetTree().GetFirstNodeInGroup("Mouse") as Mouse;
        _turnManager = GetTree().GetFirstNodeInGroup("TurnManager") as TurnManager;
    }

    public override void _Process(double delta)
    {
        if (_readyToTarget)
        {
            GlobalPosition = GetGlobalMousePosition();
            UpdateHighlights();
            CheckInput();
        }   
    }

    public override void _ExitTree()
    {
        ClearHighlights();
    }

    private void UpdateHighlights()
    {
        Enemy primaryTarget = CheckTarget();
        
        // Only skip recalculation if primary target hasn't changed and remains valid
        if (primaryTarget == _lastHoveredTarget && IsInstanceValid(_lastHoveredTarget)) return;
        _lastHoveredTarget = primaryTarget;

        HashSet<Enemy> newHighlights = GetAoETargets(primaryTarget);

        // Turn OFF highlights for enemies leaving the AoE radius
        foreach (var enemy in _highlightedEnemies)
        {
            if (!newHighlights.Contains(enemy) && IsInstanceValid(enemy))
            {
                enemy.SetHovered(false);
            }
        }

        // Always force ON for all current AoE targets to prevent mouse-hover overrides
        foreach (var enemy in newHighlights)
        {
            if (IsInstanceValid(enemy))
            {
                enemy.SetHovered(true);
            }
        }

        _highlightedEnemies = newHighlights;
    }

    private void ClearHighlights()
    {
        foreach (var enemy in _highlightedEnemies)
        {
            if (IsInstanceValid(enemy))
            {
                enemy.SetHovered(false);
            }
        }
        _highlightedEnemies.Clear();
    }

    private void CheckInput()
    {
        if (Input.IsActionJustPressed("lClick"))
        {
            Enemy target = CheckTarget();
    
            if (target != null)
            {
                HashSet<Enemy> targets = GetAoETargets(target);
                Cast(targets);
            }
            else
            {
                FlashRed();
                GD.Print("Invalid target");
            }
        }
    }

    public void Setup(Card.Element ele, Dictionary<string, Variant> data, string cardID, CardEffect.EffectType type)
    {
        _sprite = GetNode<Sprite2D>("Sprite2D");

        string path = "res://assets/interface/Target.png";
        Texture2D texture = GD.Load<Texture2D>(path);

        _sprite.Texture = texture;
        _sprite.SelfModulate = new Color(1f, 1f, 1f, 0.5f);
        GD.Print(_data);
        _data = data;
        _cardID = cardID;
        _element = ele;
        _effectType = type;

        if (_data.TryGetValue("splashTiles", out Variant splash))
        {
            _splashTiles = int.Parse(_data["splashTiles"].ToString());
        }

        ZIndex = 3;
        _readyToTarget = true;
    }

    private Enemy CheckTarget()
    {
        return _mouse.GetHoveredEnemy();
    }

    private HashSet<Enemy> GetAoETargets(Enemy primaryTarget)
    {
        HashSet<Enemy> targets = new HashSet<Enemy>();
        if (!IsInstanceValid(primaryTarget)) return targets;

        targets.Add(primaryTarget);

        if (_splashTiles > 0 && _turnManager != null)
        {
            // Use logical grid cell instead of raw world position to prevent animation desync
            Vector2I primaryCell = primaryTarget.CurrentCell;
            
            var allEnemies = GetTree().GetNodesInGroup("Enemy");

            foreach (Node node in allEnemies)
            {
                if (node is Enemy enemy && enemy != primaryTarget && enemy.CurrentHealth > 0)
                {
                    Vector2I enemyCell = enemy.CurrentCell;
                    
                    int dx = primaryCell.X - enemyCell.X;
                    int dy = primaryCell.Y - enemyCell.Y;

                    // Screen-Space Isometric Diamond Distance
                    int screenX = Mathf.Abs(dx - dy);
                    int screenY = Mathf.Abs(dx + dy);
                    int dist = (screenX + screenY) / 2;

                    // NOTE: If you want an 8-directional box AoE instead of a diamond, replace 'dist' with:
                    // int dist = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
                    
                    if (dist <= _splashTiles)
                    {
                        targets.Add(enemy);
                    }
                }
            }
        }

        return targets;
    }

    private void Cast(HashSet<Enemy> targets)
    {
        int damage = 0;
        if (_effectType == CardEffect.EffectType.EnemyDamage && _data.ContainsKey("damage"))
        {
            damage = int.Parse(_data["damage"].ToString());
        }

        PackedScene statusScene = null;
        if (_effectType == CardEffect.EffectType.StatusEffect)
        {
            statusScene = GD.Load<PackedScene>("res://prefabs/statusEffect.tscn");
        }

        foreach (Enemy target in targets)
        {
            if (!IsInstanceValid(target)) continue;

            switch (_effectType)
            {
                case CardEffect.EffectType.EnemyDamage:
                    GD.Print($"Casting {_cardID} on {target.Name} for {damage} damage");
                    target.TakeDamage(damage);
                    break;
                    
                case CardEffect.EffectType.StatusEffect:
                    if (statusScene != null)
                    {
                        StatusEffect statusEffect = statusScene.Instantiate() as StatusEffect;
                        statusEffect.Setup(_data, _element);
                        target.AddChild(statusEffect);
                    }
                    break;
                    
                default:
                    GD.PushWarning("EFFECT TYPE NO WORK");
                    break;
            }
        }

        _readyToTarget = false;
        ClearHighlights();
        QueueFree();
    }

    private async void FlashRed()
    {
        if (_sprite == null) return;

        Color original = _sprite.SelfModulate;
        Tween tween = CreateTween();

        tween.TweenProperty(_sprite, "self_modulate", Colors.Red, 0.1f);
        tween.TweenProperty(_sprite, "self_modulate", original, 0.1f);

        await ToSignal(tween, Tween.SignalName.Finished);
    }
}