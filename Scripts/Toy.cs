using Godot;
using System;

// ==============================================================================
// ! TOY.CS ! - Individual Toy Entity
// ==============================================================================
// PURPOSE: Represents a single toy on the conveyor belt.
//          - Moves automatically toward TargetX position
//          - Can be interacted with to display dialogue
//          - Can be dismissed (exported or discarded) with animations
//
// DEBUGGING TIPS:
//   - Check ToyKey: even = good toy, odd = evil toy
//   - Check _isStopped to see if toy has reached target
//   - Check TargetX to verify expected stop position
//   - TargetX = 99999 indicates toy has been dismissed
// ==============================================================================

/// <summary>
/// Toy: Represents a single toy entity on the conveyor belt.
/// Handles movement, interaction, and dismissal animations.
/// </summary>
public partial class Toy : Area2D
{
    // =========================================================================
    // ! EXPORTED PROPERTIES !
    // =========================================================================
    
    [Export] public float Speed = 100.0f;           // Movement speed in pixels/second
    
    // =========================================================================
    // ! PUBLIC PROPERTIES !
    // =========================================================================
    
    public string CurrentThought = "Hello";         // Dialogue text displayed on interaction
    public int ToyKey = 0;                          // Key value: even = good, odd = evil
    public float TargetX = 160.0f;                  // X coordinate where toy stops

    // =========================================================================
    // ! SIGNALS !
    // =========================================================================
    
    [Signal] public delegate void ToyInteractedEventHandler(Toy toy, string thought);
    [Signal] public delegate void ToyStoppedEventHandler();

    // =========================================================================
    // ! PRIVATE STATE !
    // =========================================================================
    
    private bool _isStopped = false;                // True when toy has reached target
    private float _previousTargetX = 0f;            // Used to detect TargetX changes (queue shift)

    // =========================================================================
    // ! PHYSICS PROCESS ! - Movement Logic
    // =========================================================================

    /// <summary>
    /// Handles toy movement toward TargetX each physics frame.
    /// Automatically resumes movement if TargetX increases (queue shifted forward).
    /// </summary>
    public override void _PhysicsProcess(double delta)
    {
        // Check if TargetX was increased (queue shifted) - resume movement
        if (_isStopped && TargetX > _previousTargetX && Position.X < TargetX)
        {
            _isStopped = false;
        }
        _previousTargetX = TargetX;
        
        // Only process movement if not stopped
        if (!_isStopped)
        {
            // Calculate movement for this frame
            float moveAmount = (float)(Speed * delta);
            
            // Check if movement would overshoot target
            if (Position.X + moveAmount >= TargetX)
            {
                // Snap to target and stop
                Position = new Vector2(TargetX, Position.Y);
                _isStopped = true;
                EmitSignal(SignalName.ToyStopped);
            }
            else
            {
                // Continue moving right
                Position += new Vector2(moveAmount, 0);
            }
        }
    }

    // =========================================================================
    // ! INTERACTION !
    // =========================================================================

    /// <summary>
    /// Called when player presses Z while facing this toy.
    /// Emits signal for FactoryManager to handle dialogue.
    /// </summary>
    public void Interact()
    {
        // Stop movement and emit interaction signal
        _isStopped = true;
        EmitSignal(SignalName.ToyInteracted, this, CurrentThought);
    }

    // =========================================================================
    // ! DISMISSAL ANIMATIONS !
    // =========================================================================

    /// <summary>
    /// Dismisses the toy with appropriate animation.
    /// Export: Moves up and fades out at conveyor speed.
    /// Discard: Slides right and fades out into bin.
    /// </summary>
    /// <param name="exported">True for export animation, false for discard.</param>
    public void Dismiss(bool exported)
    {
        // Disable collision to prevent further interactions
        GetNode<CollisionShape2D>("CollisionShape2D").SetDeferred("disabled", true);
        
        // Allow animation movement
        _isStopped = false;
        
        if (exported)
        {
            // --- EXPORT ANIMATION ---
            // Move UP and fade out at conveyor speed (100 pixels/sec = 2 seconds for 200px)
            Speed = 0f;
            TargetX = 99999f; // Mark as dismissed
            
            float distance = 200f;              // Distance to travel up
            float duration = distance / 100f;   // Match conveyor speed timing
            
            var tween = CreateTween();
            tween.SetParallel(true);
            tween.TweenProperty(this, "position", Position + new Vector2(0, -distance), duration);
            tween.TweenProperty(this, "modulate", new Color(1, 1, 1, 0), duration);
            tween.SetParallel(false);
            tween.TweenCallback(Callable.From(QueueFree));
        }
        else
        {
            // --- DISCARD ANIMATION ---
            // Slide right (into bin) and fade out
            Speed = 0f;
            TargetX = 99999f; // Mark as dismissed
            
            var tween = CreateTween();
            tween.SetParallel(true);
            // Move 60 pixels to the right (into bin)
            tween.TweenProperty(this, "position", Position + new Vector2(60, 0), 0.5f);
            // Fade to transparent
            tween.TweenProperty(this, "modulate", new Color(1, 1, 1, 0), 0.5f);
            tween.SetParallel(false);
            tween.TweenCallback(Callable.From(QueueFree));
        }
    }
}