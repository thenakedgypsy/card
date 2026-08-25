using Godot;
using System;
using Godot.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class Summon : Node2D, IHealth
{
	[Signal]
	public delegate void TurnFinishedEventHandler(Summon summon);

	[Export] public bool AttacksEnemies = false;
	[Export] public int AttackDamage = 1;
	[Export] public int AttackRange = 2; // tiles

	public int Health;
	public int CurrentHealth;
	
	public int? TurnsActive = null;
	public Card.Element Element;
	private Sprite2D _sprite;
	private HealthBar _healthBar;
	private Line2D _line;

	private int _drawLineRequestId = 0;
	private TurnManager _turnManager;
	private List<Godot.Collections.Dictionary<string, Variant>> _attackEffects = new();

	// Hover logic
	private bool _isMouseHovered = false;
	private bool _isAoEHovered = false;

	public override void _Ready()
	{
		_turnManager = GetTree().GetFirstNodeInGroup("TurnManager") as TurnManager;
		_sprite = GetNode<Sprite2D>("Sprite2D");
		_healthBar = GetNode<HealthBar>("HealthBar");
		_line = GetNodeOrNull<Line2D>("Line2D");

		if (_line == null)
		{
			_line = new Line2D();
			_line.Name = "Line2D";
			_line.ZIndex = 100;
			_line.Width = 2f;
			_line.DefaultColor = Colors.White;
			_line.Points = new Vector2[] { Vector2.Zero, Vector2.Zero };
			_line.Visible = false;
			AddChild(_line);
		}

		AddToGroup("Summons");
	}

	public override void _Process(double delta)
	{
	}

	// --- HOVER & VISUAL HIGHLIGHT LOGIC ---

	public void SetHovered(bool hovered)
	{
		_isAoEHovered = hovered;
		UpdateVisualState();
	}

	public void SetMouseHovered(bool hovered)
	{
		_isMouseHovered = hovered;
		UpdateVisualState();
	}

	public void UpdateVisualState()
	{
		if (_sprite == null || !GodotObject.IsInstanceValid(_sprite))
			return;

		bool isHighlighted = _isMouseHovered || _isAoEHovered;
		_sprite.SelfModulate = isHighlighted ? Colors.Yellow : Colors.White;
	}

	public void MouseOver()
	{
		SetMouseHovered(true);
		Mouse mouse = GetTree().GetFirstNodeInGroup("Mouse") as Mouse;
		if (mouse != null) mouse.SetHoveredSummon(this);
	}

	public void MouseOff()
	{
		SetMouseHovered(false);
		Mouse mouse = GetTree().GetFirstNodeInGroup("Mouse") as Mouse;
		if (mouse != null && mouse.GetHoveredSummon() == this)
		{
			mouse.SetHoveredSummon(null);
		}
	}

	private void _on_area_2d_mouse_entered()
	{
		MouseOver();
	}

	private void _on_area_2d_mouse_exited()
	{
		MouseOff();
	}

	public bool IsHovered() => _isMouseHovered || _isAoEHovered;

	// --- DRAW LINE & ATTACK LOGIC ---

	public async void DrawLineBetween(Vector2 target, float width = 2f)
	{
		if (_line == null)
			return;

		Color color = Element switch
		{
			Card.Element.Fire => Colors.Red,
			Card.Element.Water => Colors.Blue,
			Card.Element.Earth => Colors.Green,
			Card.Element.Wind => Colors.LightBlue,
			_ => Colors.Gray
		};

		float spriteHeight = (_sprite.Texture?.GetHeight() ?? 0f) * _sprite.Scale.Y;
		Vector2 spriteTop = _sprite.GlobalPosition + new Vector2(0, -(spriteHeight - 32));

		Vector2 localStart = spriteTop - GlobalPosition;
		Vector2 localEnd = target - GlobalPosition;

		_line.Points = new Vector2[] { localStart, localEnd };
		_line.Width = width;
		_line.DefaultColor = color;
		_line.ZIndex = 100;
		_line.Visible = true;

		int requestId = ++_drawLineRequestId;
		await ToSignal(GetTree().CreateTimer(0.15f), SceneTreeTimer.SignalName.Timeout);
		if (requestId == _drawLineRequestId)
		{
			ClearDrawLine();
		}
	}

	public void ClearDrawLine()
	{
		if (_line == null)
			return;

		_line.Visible = false;
	}

	public async Task TakeTurn()
	{
		if (!AttacksEnemies)
		{
			EndTurn();
			return;
		}

		Enemy nearestEnemy = null;
		int minDistance = int.MaxValue;

		Vector2I myCell = TurnManager.Instance.WorldToCell(GlobalPosition);

		var enemies = GetTree().GetNodesInGroup("Enemies");
		foreach (Node node in enemies)
		{
			if (node is Enemy enemy && GodotObject.IsInstanceValid(enemy))
			{
				Vector2I enemyCell = TurnManager.Instance.WorldToCell(enemy.GlobalPosition);
				int dist = TurnManager.Instance.TileDistance(myCell, enemyCell);

				if (dist < minDistance)
				{
					minDistance = dist;
					nearestEnemy = enemy;
				}
			}
		}

		if (nearestEnemy != null && minDistance <= AttackRange)
		{
			await Attack(nearestEnemy);
		}

		EndTurn();
	}

	private async Task Attack(Enemy enemy)
	{
		if (!GodotObject.IsInstanceValid(enemy)) return;

		GD.Print($"[{Name}] ATTACK → '{enemy.Name}'");
		FlashRed();        
		DrawLineBetween(enemy.GlobalPosition, 5f);    

		if (_attackEffects.Count > 0)
		{
			PackedScene scene = GD.Load<PackedScene>("res://prefabs/SpellTargeter.tscn");
			SpellTargeter targeter = scene.Instantiate() as SpellTargeter;

			GetParent().AddChild(targeter);
			await targeter.SetupAutoCast(Element, _attackEffects, Name, enemy);
			GD.Print("Autocasting ", _attackEffects);
		}
		else
		{
			enemy.TakeDamage(AttackDamage, Element);                
		}               
	}

	private void EndTurn()
	{
		EmitSignal(SignalName.TurnFinished, this);
	}

	public async void FlashRed()
	{
		Color original = SelfModulate;
		Tween tween = CreateTween();
		tween.TweenProperty(_sprite, "self_modulate", Colors.Red, 0.1f);
		tween.TweenProperty(_sprite, "self_modulate", original, 0.1f);
		await ToSignal(tween, Tween.SignalName.Finished);
	}

	public void Generate(Card.Element ele, Godot.Collections.Dictionary<string, Variant> data, string summonID)
	{
		Element = ele;
		Health = data.ContainsKey("health") ? data["health"].AsInt32() : 10;
		CurrentHealth = Health;
		AttackRange = data.ContainsKey("range") ? data["range"].AsInt32() : 1;
		AttacksEnemies = data.ContainsKey("attacksEnemies") && data["attacksEnemies"].AsBool();
		Name = summonID;

		_attackEffects.Clear();

		if (data.ContainsKey("effects") && data["effects"].VariantType == Variant.Type.Array)
		{
			var rawEffects = data["effects"].AsGodotArray();
			foreach (var item in rawEffects)
			{
				_attackEffects.Add(item.AsGodotDictionary<string, Variant>());
			}
		}
		else
		{
			GD.PushWarning($"Summon missing effects[] in {summonID} json");
		}

		string artToLoad = summonID;
		if (data != null && data.ContainsKey("artId"))
		{
			artToLoad = data["artId"].ToString();
		}

		string path = $"res://assets/summons/{artToLoad}.png";
		Texture2D texture = GD.Load<Texture2D>(path);
		
		if (texture == null)
		{
			GD.PrintErr($"Summon: Failed to load texture at {path}");
		}

		_sprite.Texture = texture;

		UpdateVisualBounds();
	}

	public void TakeDamage(int value)
	{
		CurrentHealth -= value;
		GD.Print($"Summon takes {value} damage");

		PackedScene scene = GD.Load<PackedScene>("res://prefabs/floating_damage_number.tscn");
		FloatingDamageNumber fdn = scene.Instantiate() as FloatingDamageNumber;
		
		AddChild(fdn);
		fdn.Appear(value, Card.Element.Earth);

		FlashRed();

		if (CurrentHealth <= 0)
		{
			GD.Print("IS DESTROYED");

			RemoveFromGroup("Summons");

			if (TurnManager.Instance != null)
			{
				TurnManager.Instance.RebakeNav();
			}

			SetProcess(false);
			SetPhysicsProcess(false);
			SetDeferred("monitoring", false);
			CallDeferred(Node.MethodName.QueueFree);
		}
	}

	public float GetMaxHealth() => Health;
	public float GetCurrentHealth() => CurrentHealth;

	private void UpdateVisualBounds()
	{
		if (_sprite.Texture == null)
			return;

		Vector2 spriteSize = _sprite.Texture.GetSize() * _sprite.Scale;

		if (_sprite.Centered)
		{
			_sprite.Offset = new Vector2(0, 16f - (spriteSize.Y * 0.5f));
		}
		else
		{
			_sprite.Offset = new Vector2(0, 16f - spriteSize.Y);
		}

		if (_healthBar != null)
		{
			Vector2 barSize = _healthBar.Size * _healthBar.Scale;
			const float padding = 4f;

			float topOfSprite = _sprite.Centered 
				? _sprite.Offset.Y - (spriteSize.Y * 0.5f) 
				: _sprite.Offset.Y;

			_healthBar.Position = new Vector2(-barSize.X * 0.5f, topOfSprite - padding - barSize.Y);
		}

		// --- AREA2D CAPSULTE COLLISION RESIZING ---
		Area2D area = GetNodeOrNull<Area2D>("Area2D");
		if (area != null)
		{
		    CollisionShape2D collisionShape = area.GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		    if (collisionShape != null)
		    {
		        // Duplicate if it's already a capsule, or instantiate a new one if switching shape types
		        CapsuleShape2D newCapsule = collisionShape.Shape is CapsuleShape2D capsuleShape
		            ? capsuleShape.Duplicate() as CapsuleShape2D
		            : new CapsuleShape2D();

		        float radius = spriteSize.X * 0.5f;
		        float height = Mathf.Max(spriteSize.Y, radius * 2f); // Godot enforces height >= 2 * radius

		        newCapsule.Radius = radius;
		        newCapsule.Height = height;

		        collisionShape.Shape = newCapsule;
		        collisionShape.Scale = Vector2.One;
		        collisionShape.GlobalPosition = GlobalPosition + new Vector2(0, -50);
		    }
		}
	}
}