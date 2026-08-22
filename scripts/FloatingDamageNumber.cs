using Godot;
using System;

public partial class FloatingDamageNumber : Node2D
{
	public RichTextLabel label;

    private Card.Element damageElement;

    private int damageNumber;

    public Tween tween;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	 	label = GetNode<RichTextLabel>("RichTextLabel");
        
        GD.Print($"{damageElement} Card Element");
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.

	public void Appear(int number, Card.Element element) //placeholder ele
	{
        damageNumber = number;
        damageElement = element;
		label.Text = _getTextString();
		Animate();
        GD.Print($"{_getTextString()}");
	}

	public void Animate()
    {
        // 1. Create a Tween bound to this node
        tween = CreateTween();

        // Randomize horizontal trajectory
        float randomXOffset = (float)GD.RandRange(-50.0f, 50.0f); // Random left/right arc
        Vector2 startPos = Position;
        Vector2 targetPos = startPos + new Vector2(randomXOffset, -30); // Final resting position
        float arcHeight = 45.0f; // Apex peak height
        float totalDuration = 1.5f;
        
        // Arc movement with earlier apex peak
        tween.TweenMethod(Callable.From<float>((t) => 
        {
            // Fast-forward initial vertical progress so it arcs earlier
            float skewedT = Mathf.Pow(t, 0.6f); 
            
            // Base linear position
            Vector2 currentPos = startPos.Lerp(targetPos, t);
            
            // Parabolic arc applied using the skewed progression
            float heightOffset = 4 * arcHeight * skewedT * (1.0f - skewedT);
            currentPos.Y -= heightOffset;
            
            Position = currentPos;
        }), 0.0f, 1.0f, totalDuration)
        .SetTrans(Tween.TransitionType.Linear);
        
        // Fade out timing
        tween.Parallel().TweenProperty(this, "modulate:a", 0.0f, 0.5f)
             .SetDelay(0.5f)
             .SetTrans(Tween.TransitionType.Quad)
             .SetEase(Tween.EaseType.Out);

        // 3. Free the node automatically when the animations finish
        tween.Chain().TweenCallback(Callable.From(QueueFree));
    }

    public string _getTextString()
    {
        switch (damageElement)
        {
            case Card.Element.Earth:
                return $"[center][color=#{Colors.Green.ToHtml()}]{damageNumber} [img=16x16]res://assets/interface/brown.png[/img]";
            case Card.Element.Wind:
                return $"[center][color=#{Colors.White.ToHtml()}]{damageNumber} [img=16x16]res://assets/interface/pale.png[/img]";
            case Card.Element.Fire:
                return $"[center][color=#{Colors.Orange.ToHtml()}]{damageNumber} [img=16x16]res://assets/interface/orange.png[/img]";
            case Card.Element.Water:
                return $"[center][color=#{Colors.Blue.ToHtml()}]{damageNumber} [img=16x16]res://assets/interface/blue.png[/img]";
            case Card.Element.Neutral:
                //will need a neutral colour/image
                return $"[center][color=#{Colors.Gray.ToHtml()}]{damageNumber} [img=16x16]res://assets/interface/pale.png[/img]";
            default:
            return $"[center][color=#{Colors.Gray.ToHtml()}]{damageNumber} [img=16x16]res://assets/interface/pale.png[/img]";
        }       
    }
}