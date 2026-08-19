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
    private List<Enemy> _highlightedEnemies = new List<Enemy>();
    private Enemy _lastHoveredTarget; // Caches target to prevent calculating highlights every frame

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
        // Ensures highlights clean up safely when the spell is cast or canceled
        ClearHighlights();
    }

    private void UpdateHighlights()
    {
        Enemy primaryTarget = CheckTarget();
        
        // Performance optimization: Only recalculate AoE highlights if our primary target has changed
        if (primaryTarget == _lastHoveredTarget) return;
        _lastHoveredTarget = primaryTarget;

        List<Enemy> newHighlights = GetAoETargets(primaryTarget);

        // Turn OFF highlights for enemies that are no longer targeted
        foreach (var enemy in _highlightedEnemies)
        {
            if (!newHighlights.Contains(enemy) && IsInstanceValid(enemy))
            {
                enemy.SetHovered(false);
            }
        }

        // Turn ON highlights for the new targets
        foreach (var enemy in newHighlights)
        {
            if (!_highlightedEnemies.Contains(enemy) && IsInstanceValid(enemy))
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
        if(Input.IsActionJustPressed("lClick"))
        {
            Enemy target = CheckTarget();
    
            if(target != null)
            {
                List<Enemy> targets = GetAoETargets(target);
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

        _data = data;
        _cardID = cardID;
        _element = ele;
        _effectType = type;

        // Extract splash radius
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

    private List<Enemy> GetAoETargets(Enemy primaryTarget)
    {
        List<Enemy> targets = new List<Enemy>();
        if (primaryTarget == null) return targets;

        // Always target the primary enemy
        targets.Add(primaryTarget);

        // Add splash targets if radius > 0
        if (_splashTiles > 0 && _turnManager != null)
        {
            Vector2I primaryCell = _turnManager.WorldToCell(primaryTarget.GlobalPosition);
            
            // Fetches all enemy nodes without relying strictly on Godot Groups
            List<Enemy> allEnemies = GetAllEnemies(GetTree().Root);

            foreach (Enemy enemy in allEnemies)
            {
                if (enemy != primaryTarget && enemy.CurrentHealth > 0)
                {
                    Vector2I enemyCell = _turnManager.WorldToCell(enemy.GlobalPosition);
                    
                    // Manhattan distance calculates our horizontal/vertical tile radius
                    int dist = Mathf.Abs(primaryCell.X - enemyCell.X) + Mathf.Abs(primaryCell.Y - enemyCell.Y);
                    
                    if (dist <= _splashTiles)
                    {
                        targets.Add(enemy);
                    }
                }
            }
        }

        return targets;
    }

    // Helper to recursively find enemies in the scene without needing an explicit Group assignment
    private List<Enemy> GetAllEnemies(Node node)
    {
        List<Enemy> enemies = new List<Enemy>();
        foreach (Node child in node.GetChildren())
        {
            if (child is Enemy enemy)
            {
                enemies.Add(enemy);
            }
            enemies.AddRange(GetAllEnemies(child)); // Recurse
        }
        return enemies;
    }

    private void Cast(List<Enemy> targets)
    {
        // Parse damage once to avoid repeating it in the loop
        int damage = 0;
        if (_effectType == CardEffect.EffectType.EnemyDamage && _data.ContainsKey("damage"))
        {
            damage = int.Parse(_data["damage"].ToString());
        }

        // Cache the status prefab if needed
        PackedScene statusScene = null;
        if (_effectType == CardEffect.EffectType.StatusEffect)
        {
            statusScene = GD.Load<PackedScene>("res://prefabs/statusEffect.tscn");
        }

        // Apply effect to all validated targets
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
                
                        GD.Print($"Casting {_cardID} on {target.Name} for {statusEffect}");

                        target.AddChild(statusEffect);
                        GD.Print($"Added status{statusEffect} effect to target{target}");
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