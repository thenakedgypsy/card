using Godot;
using System;

public partial class FloatingDamageNumber : Node2D
{
	public RichTextLabel label;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	 	label = GetNode<RichTextLabel>("RichTextLabel");
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.

	public void Appear(int number, Card.Element element)
	{
		label.Text = $"[center]{number}";
		Animate();
	}

	public void Animate()
    {
        // 1. Create a Tween bound to this node
        Tween tween = CreateTween();

        // 2. Run float up and fade out at the same time
        tween.SetParallel(true);

        // Move upward by 50 pixels over 0.8 seconds
        tween.TweenProperty(this, "position", Position + new Vector2(0, -50), 0.8f)
             .SetTrans(Tween.TransitionType.Quad)
             .SetEase(Tween.EaseType.In);

        // Fade out alpha over 0.8 seconds
        tween.TweenProperty(this, "modulate:a", 0.0f, 0.8f)
             .SetTrans(Tween.TransitionType.Quad)
             .SetEase(Tween.EaseType.Out);

        // 3. Free the node automatically when the animations finish
        tween.Chain().TweenCallback(Callable.From(QueueFree));
    }
}
