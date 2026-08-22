using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class SpellTargeter : Node2D
{
    private Sprite2D _sprite;
    private List<Godot.Collections.Dictionary<string, Variant>> _effects = new();
    private string _cardID;
    private Card.Element _element;

    private bool _readyToTarget;
    private Mouse _mouse;
    private TurnManager _turnManager;

    private int _splashTiles = 0;
    private HashSet<Enemy> _highlightedEnemies = new();
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

    public void Setup(Card.Element ele, List<Godot.Collections.Dictionary<string, Variant>> effects, string cardID)
    {
        _sprite = GetNode<Sprite2D>("Sprite2D");
        _sprite.Texture = GD.Load<Texture2D>("res://assets/interface/Target.png");
        _sprite.SelfModulate = new Color(1f, 1f, 1f, 0.5f);

        _effects = effects;
        _cardID = cardID;
        _element = ele;

        if (_effects.Count > 0 && _effects[0].TryGetValue("splashTiles", out Variant splash))
        {
            _splashTiles = splash.AsInt32();
        }

        ZIndex = 3;
        _readyToTarget = true;
    }

    public async Task SetupAutoCast(Card.Element ele, List<Godot.Collections.Dictionary<string, Variant>> effects, string cardID, Enemy primaryTarget)
    {
        _effects = effects;
        _cardID = cardID;
        _element = ele;

        if (_effects.Count > 0 && _effects[0].TryGetValue("splashTiles", out Variant splash))
        {
            _splashTiles = splash.AsInt32();
        }

        _readyToTarget = false; 
        
        _sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
        if (_sprite != null) _sprite.Visible = false;

        HashSet<Enemy> targets = GetAoETargets(primaryTarget);
        await CastAsync(targets);
    }

    private void CheckInput()
    {
        if (Input.IsActionJustPressed("lClick"))
        {
            Enemy target = CheckTarget();
            if (target != null)
            {
                HashSet<Enemy> targets = GetAoETargets(target);
                _ = CastAsync(targets);
            }
            else
            {
                FlashRed();
            }
        }
    }

    private async System.Threading.Tasks.Task CastAsync(HashSet<Enemy> targets)
    {
        _readyToTarget = false;
        ClearHighlights();
        if (_sprite != null) _sprite.Visible = false;

        PackedScene statusScene = GD.Load<PackedScene>("res://prefabs/statusEffect.tscn");

        for (int i = 0; i < _effects.Count; i++)
        {
            var effectData = _effects[i];

            if (!Enum.TryParse(effectData["effectType"].AsString(), out CardEffect.EffectType effectType))
                continue;

            int damage = effectData.ContainsKey("damage") ? effectData["damage"].AsInt32() : 0;

            foreach (Enemy target in targets)
            {
                if (!IsInstanceValid(target) || target.CurrentHealth <= 0) continue;

                GD.Print("checking effectType = ", effectType);
                switch (effectType)
                {
                    case CardEffect.EffectType.EnemyDamage:
                        target.TakeDamage(damage, _element);
                        break;
                        
                    case CardEffect.EffectType.StatusEffect:
                        if (statusScene != null)
                        {
                            StatusEffect statusEffect = statusScene.Instantiate() as StatusEffect;
                            GD.Print("Setting up status with data: ", effectData);
                            statusEffect.Setup(effectData, _element);
                            target.AddChild(statusEffect);
                            statusEffect.OnApplied();
                        }
                        break;
                }
            }

            if (i < _effects.Count - 1)
            {
                await ToSignal(GetTree().CreateTimer(0.30f), SceneTreeTimer.SignalName.Timeout);
            }
        }

        QueueFree();
    }

    private Enemy CheckTarget() => _mouse.GetHoveredEnemy();

    private HashSet<Enemy> GetAoETargets(Enemy primaryTarget)
    {
        HashSet<Enemy> targets = new();
        if (!IsInstanceValid(primaryTarget)) return targets;

        targets.Add(primaryTarget);

        if (_splashTiles > 0 && _turnManager != null)
        {
            Vector2I primaryCell = primaryTarget.CurrentCell;
            var allEnemies = GetTree().GetNodesInGroup("Enemy");

            foreach (Node node in allEnemies)
            {
                if (node is Enemy enemy && enemy != primaryTarget && enemy.CurrentHealth > 0)
                {
                    Vector2I enemyCell = enemy.CurrentCell;
                    int dx = primaryCell.X - enemyCell.X;
                    int dy = primaryCell.Y - enemyCell.Y;

                    int screenX = Mathf.Abs(dx - dy);
                    int screenY = Mathf.Abs(dx + dy);
                    int dist = (screenX + screenY) / 2;
                    
                    if (dist <= _splashTiles) targets.Add(enemy);
                }
            }
        }

        return targets;
    }

    private void UpdateHighlights()
    {
        Enemy primaryTarget = CheckTarget();
        if (primaryTarget == _lastHoveredTarget && IsInstanceValid(_lastHoveredTarget)) return;
        _lastHoveredTarget = primaryTarget;

        HashSet<Enemy> newHighlights = GetAoETargets(primaryTarget);

        foreach (var enemy in _highlightedEnemies)
        {
            if (!newHighlights.Contains(enemy) && IsInstanceValid(enemy)) enemy.SetHovered(false);
        }

        foreach (var enemy in newHighlights)
        {
            if (IsInstanceValid(enemy)) enemy.SetHovered(true);
        }

        _highlightedEnemies = newHighlights;
    }

    private void ClearHighlights()
    {
        foreach (var enemy in _highlightedEnemies)
        {
            if (IsInstanceValid(enemy)) enemy.SetHovered(false);
        }
        _highlightedEnemies.Clear();
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