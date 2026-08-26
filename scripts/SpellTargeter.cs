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

    private HashSet<Node2D> _highlightedTargets = new();
    private Node2D _lastHoveredTarget;

    public override void _Ready()
    {
        _readyToTarget = true;
        _mouse = GetTree().GetFirstNodeInGroup("Mouse") as Mouse;
        _turnManager = GetTree().GetFirstNodeInGroup("TurnManager") as TurnManager;

        if (_turnManager != null) _turnManager.IsResolving = true;
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
        if (_turnManager != null) _turnManager.IsResolving = false;
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

        ZIndex = 3;
        _readyToTarget = true;
    }

    public async Task SetupAutoCast(Card.Element ele, List<Godot.Collections.Dictionary<string, Variant>> effects, string cardID, Node2D primaryTarget)
    {
        _effects = effects;
        _cardID = cardID;
        _element = ele;

        _readyToTarget = false; 
        
        _sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
        if (_sprite != null) _sprite.Visible = false;

        await CastAsync(primaryTarget);
    }

    private void CheckInput()
    {
        if (Input.IsActionJustPressed("lClick"))
        {
            Node2D target = CheckTarget();
            if (target != null)
            {
                _ = CastAsync(target);
            }
            else
            {
                FlashRed();
            }
        }
    }

    private async Task CastAsync(Node2D primaryTarget)
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
            int splashTiles = effectData.TryGetValue("splashTiles", out Variant splash) ? splash.AsInt32() : 0;
            string targetType = GetTargetType(effectData);

            HashSet<Node2D> targets = GetAoETargets(primaryTarget, splashTiles, targetType);

            foreach (Node2D target in targets)
            {
                if (!IsInstanceValid(target)) continue;

                if (target is Enemy enemy && enemy.CurrentHealth > 0)
                {
                    switch (effectType)
                    {
                        case CardEffect.EffectType.EnemyDamage:
                            enemy.TakeDamage(damage, _element);
                            break;
                            
                        case CardEffect.EffectType.StatusEffect:
                            if (statusScene != null)
                            {
                                StatusEffect statusEffect = statusScene.Instantiate() as StatusEffect;
                                statusEffect.Setup(effectData, _element);
                                enemy.AddChild(statusEffect);
                                statusEffect.OnApplied();
                            }
                            break;
                    }
                }
                else if (target is Summon summon && summon.CurrentHealth > 0)
                {
                    switch (effectType)
                    {
                        //todo - buff/timer for summons
                        case CardEffect.EffectType.SummonModify:
                            //effect here
                            break;
                        case CardEffect.EffectType.StopEffect:
                            //effect here
                            break;
                    }
                }
            }

            if (i < _effects.Count - 1)
            {
                await ToSignal(GetTree().CreateTimer(0.30f), SceneTreeTimer.SignalName.Timeout);
            }
        }

        QueueFree();
    }

    private string GetTargetType(Godot.Collections.Dictionary<string, Variant> effectData)
    {
        if (effectData.TryGetValue("targetType", out Variant tt))
        {
            return tt.AsString();
        }
        return "Enemy";
    }

    private string GetPrimaryTargetType()
    {
        if (_effects.Count > 0)
        {
            return GetTargetType(_effects[0]);
        }
        return "Enemy";
    }

    private Node2D CheckTarget()
    {
        if (_mouse == null) return null;

        string targetType = GetPrimaryTargetType();
        switch (targetType)
        {
            case "Summon":
                return _mouse.GetHoveredSummon();
            case "Any":
            case "Both":
                return (Node2D)_mouse.GetHoveredEnemy() ?? _mouse.GetHoveredSummon();
            case "Enemy":
            default:
                return _mouse.GetHoveredEnemy();
        }
    }

    private HashSet<Node2D> GetAoETargets(Node2D primaryTarget, int splashTiles, string targetType)
    {
        HashSet<Node2D> targets = new();
        if (!IsInstanceValid(primaryTarget)) return targets;

        targets.Add(primaryTarget);

        if (splashTiles > 0 && _turnManager != null)
        {
            Vector2I primaryCell = _turnManager.WorldToCell(primaryTarget.GlobalPosition);
            
            List<Node> candidates = new();
            if (targetType == "Enemy" || targetType == "Any" || targetType == "Both")
                candidates.AddRange(GetTree().GetNodesInGroup("Enemy"));
            if (targetType == "Summon" || targetType == "Any" || targetType == "Both")
                candidates.AddRange(GetTree().GetNodesInGroup("Summons"));

            foreach (Node node in candidates)
            {
                if (node is Node2D targetNode && targetNode != primaryTarget && IsInstanceValid(targetNode))
                {
                    Vector2I nodeCell = _turnManager.WorldToCell(targetNode.GlobalPosition);
                    int dx = primaryCell.X - nodeCell.X;
                    int dy = primaryCell.Y - nodeCell.Y;

                    int screenX = Mathf.Abs(dx - dy);
                    int screenY = Mathf.Abs(dx + dy);
                    int dist = (screenX + screenY) / 2;
                    
                    if (dist <= splashTiles) targets.Add(targetNode);
                }
            }
        }

        return targets;
    }

    private int GetMaxSplashTiles()
    {
        int maxSplash = 0;
        foreach (var effect in _effects)
        {
            if (effect.TryGetValue("splashTiles", out Variant splash))
            {
                maxSplash = Math.Max(maxSplash, splash.AsInt32());
            }
        }
        return maxSplash;
    }

    private void UpdateHighlights()
    {
        Node2D primaryTarget = CheckTarget();
        if (primaryTarget == _lastHoveredTarget && IsInstanceValid(_lastHoveredTarget)) return;
        _lastHoveredTarget = primaryTarget;

        HashSet<Node2D> newHighlights = GetAoETargets(primaryTarget, GetMaxSplashTiles(), GetPrimaryTargetType());

        foreach (var target in _highlightedEnemiesToSet(_highlightedTargets))
        {
            if (!newHighlights.Contains(target) && IsInstanceValid(target)) SetTargetHovered(target, false);
        }

        foreach (var target in newHighlights)
        {
            if (IsInstanceValid(target)) SetTargetHovered(target, true);
        }

        _highlightedTargets = newHighlights;
    }

    private List<Node2D> _highlightedEnemiesToSet(HashSet<Node2D> set) => new List<Node2D>(set);

    private void SetTargetHovered(Node2D target, bool hovered)
    {
        if (target is Enemy enemy) enemy.SetHovered(hovered);
        else if (target is Summon summon) summon.SetHovered(hovered);
    }

    private void ClearHighlights()
    {
        foreach (var target in _highlightedTargets)
        {
            if (IsInstanceValid(target)) SetTargetHovered(target, false);
        }
        _highlightedTargets.Clear();
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