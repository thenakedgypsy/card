using Godot;
using System;
using System.Collections.Generic;

public partial class Mouse : Node2D
{
    private Card _activeCard;
    private bool OverBoard;
    private Enemy HoveredEnemy;

    public override void _Ready()
    {
        AddToGroup("Mouse");
    }

    public override void _Process(double delta)
    {
        GlobalPosition = GetGlobalMousePosition();
        UpdateHoveredEnemy();
        HandleInput();
    }

    // =========================
    // ENEMY HOVER MANAGEMENT
    // =========================

    private void UpdateHoveredEnemy()
    {
        var space = GetWorld2D().DirectSpaceState;

        var query = new PhysicsPointQueryParameters2D
        {
            Position = GlobalPosition,
            CollideWithAreas = true,
            CollideWithBodies = true
        };

        var results = space.IntersectPoint(query);

        Enemy bestEnemy = null;
        int highestZ = int.MinValue;
        float closestDistSq = float.MaxValue;

        foreach (var hit in results)
        {
            var colliderObj = hit["collider"].AsGodotObject();

            if (colliderObj is Node node)
            {
                // Check if the node itself or its parent is an Enemy
                Enemy enemy = node as Enemy ?? node.GetParent() as Enemy;

                if (enemy != null && GodotObject.IsInstanceValid(enemy) && enemy.CurrentHealth > 0)
                {
                    int zIndex = enemy.ZIndex;
                    float distSq = GlobalPosition.DistanceSquaredTo(enemy.GlobalPosition);

                    // Pick top-most ZIndex; break ties using distance to cursor
                    if (zIndex > highestZ || (zIndex == highestZ && distSq < closestDistSq))
                    {
                        highestZ = zIndex;
                        closestDistSq = distSq;
                        bestEnemy = enemy;
                    }
                }
            }
        }

        // Only update if the target enemy changed
        if (HoveredEnemy != bestEnemy)
        {
            if (GodotObject.IsInstanceValid(HoveredEnemy))
            {
                HoveredEnemy.SetHovered(false);
            }

            HoveredEnemy = bestEnemy;

            if (GodotObject.IsInstanceValid(HoveredEnemy))
            {
                HoveredEnemy.SetHovered(true);
            }
        }
    }

    // =========================
    // INPUT HANDLING
    // =========================

    private void HandleInput()
    {
        // Don't pick up cards if active spell targeting is underway
        if (GetTree().GetNodesInGroup("SpellTargeter").Count > 0)
            return;

        // Mouse pressed → pick a card under cursor
        if (Input.IsActionJustPressed("lClick"))
        {
            _activeCard = GetCardUnderMouse();

            if (_activeCard != null)
            {
                _activeCard.StartDrag();
            }
        }

        // Mouse held → drag active card
        if (Input.IsActionPressed("lClick") && _activeCard != null)
        {
            _activeCard.UpdateDrag(GlobalPosition);
        }

        // Mouse released → drop card
        if (Input.IsActionJustReleased("lClick") && _activeCard != null)
        {
            _activeCard.EndDrag();
            _activeCard = null;
        }
    }

    // =========================
    // CARD PICKING
    // =========================

    private Card GetCardUnderMouse()
    {
        var space = GetWorld2D().DirectSpaceState;

        var query = new PhysicsPointQueryParameters2D
        {
            Position = GlobalPosition,
            CollideWithAreas = true,
            CollideWithBodies = false
        };

        var results = space.IntersectPoint(query);

        Card topCard = null;
        int highestZ = int.MinValue;

        foreach (var hit in results)
        {
            var colliderObj = hit["collider"].AsGodotObject();

            if (colliderObj is Node collider)
            {
                Card card = collider as Card ?? collider.GetParent() as Card;

                if (card != null && card.ZIndex > highestZ)
                {
                    highestZ = card.ZIndex;
                    topCard = card;
                }
            }
        }

        return topCard;
    }

    public void setOverBoard(bool value) => OverBoard = value;
    public bool getOverBoard() => OverBoard;

    public void SetHoveredEnemy(Enemy enemy)
    {
        if (HoveredEnemy != enemy)
        {
            if (GodotObject.IsInstanceValid(HoveredEnemy)) HoveredEnemy.SetHovered(false);
            HoveredEnemy = enemy;
            if (GodotObject.IsInstanceValid(HoveredEnemy)) HoveredEnemy.SetHovered(true);
        }
    }

    public Enemy GetHoveredEnemy()
    {
        if (GodotObject.IsInstanceValid(HoveredEnemy) && HoveredEnemy.CurrentHealth > 0)
        {
            return HoveredEnemy;
        }
        return null;
    }
}