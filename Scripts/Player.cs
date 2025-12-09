using Godot;
using System;

// ==============================================================================
// ! PLAYER.CS ! - Player Character Controller
// ==============================================================================
// PURPOSE: Handles player movement, animation, and interactions.
//          - WASD/Arrow key movement with cardinal direction animation
//          - RayCast2D for detecting interactable objects (Toy, Door)
//          - Walk and bump sound effects
//          - Can be frozen by FactoryManager during dialogues
//
// DEBUGGING TIPS:
//   - Check IsFrozen if player won't move
//   - Check InteractionRay.IsColliding() for interaction issues
//   - Check _lastFacing for animation direction
//   - Verify CollideWithAreas is true for RayCast2D
// ==============================================================================

/// <summary>
/// Player: Character controller for movement, animation, and interaction.
/// </summary>
public partial class Player : CharacterBody2D
{
    // =========================================================================
    // ! EXPORTED PROPERTIES !
    // =========================================================================
    
    [Export] public float Speed = 150.0f;                   // Movement speed in pixels/second
    [Export] public RayCast2D InteractionRay;               // Ray for detecting interactables
    [Export] public Area2D InteractionArea;                 // Area for detecting close-range interactables
    [Export] public AnimatedSprite2D AnimatedSprite;        // Character sprite animations
    [Export] public AudioStreamPlayer SFXWalk;              // Walking sound (looping)
    [Export] public AudioStreamPlayer SFXBump;              // Collision with wall sound

    // =========================================================================
    // ! PUBLIC STATE ! (Controlled by FactoryManager)
    // =========================================================================
    
    public bool IsFrozen = false;   // When true, disables all player input and movement

    // =========================================================================
    // ! PRIVATE STATE !
    // =========================================================================
    
    private string _lastFacing = "down";    // Last cardinal direction: "up", "down", "left", "right"
    private bool _wasMoving = false;        // Tracks movement state for sound transitions

    // =========================================================================
    // ! LIFECYCLE METHODS !
    // =========================================================================

    /// <summary>
    /// Initializes player on scene entry.
    /// Configures raycast and sets initial animation.
    /// </summary>
    public override void _Ready()
    {
        // Configure raycast to ignore player's own body
        if (InteractionRay != null)
        {
            InteractionRay.AddException(this);
            // Enable collision with Area2D nodes (Door, Toy)
            InteractionRay.CollideWithAreas = true;
        }
        
        // Start facing down in idle
        ResetToIdleDown();
    }
    
    /// <summary>
    /// Resets player to idle down state.
    /// Called at shift start and after transitions.
    /// </summary>
    public void ResetToIdleDown()
    {
        _lastFacing = "down";
        _wasMoving = false;
        if (AnimatedSprite != null && AnimatedSprite.SpriteFrames.HasAnimation("idle_down"))
        {
            AnimatedSprite.Play("idle_down");
        }
    }

    // =========================================================================
    // ! PHYSICS PROCESS ! - Movement and Animation
    // =========================================================================

    /// <summary>
    /// Handles movement input, physics, animation, and sound each physics frame.
    /// </summary>
    public override void _PhysicsProcess(double delta)
    {
        // --- FREEZE CHECK ---
        // When frozen, play idle animation and stop sounds
        if (IsFrozen)
        {
            PlayAnimation(false);
            StopWalkSound();
            return;
        }

        // --- GATHER INPUT ---
        Vector2 inputDir = Vector2.Zero;

        if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up))
        {
            inputDir.Y -= 1;
        }
        if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down))
        {
            inputDir.Y += 1;
        }
        if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left))
        {
            inputDir.X -= 1;
        }
        if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right))
        {
            inputDir.X += 1;
        }

        // --- CALCULATE VELOCITY ---
        inputDir = inputDir.Normalized();
        Velocity = inputDir * Speed;
        
        // Store velocity before collision for bump detection
        Vector2 velocityBeforeCollision = Velocity;
        
        // --- PHYSICS MOVEMENT ---
        MoveAndSlide();
        
        // --- BUMP DETECTION ---
        // Play bump sound when colliding with StaticBody2D (walls)
        if (GetSlideCollisionCount() > 0 && velocityBeforeCollision.Length() > 0)
        {
            for (int i = 0; i < GetSlideCollisionCount(); i++)
            {
                var collision = GetSlideCollision(i);
                if (collision.GetCollider() is StaticBody2D)
                {
                    if (SFXBump != null && !SFXBump.Playing)
                    {
                        SFXBump.Play();
                    }
                    break;
                }
            }
        }
        
        // --- WALK SOUND MANAGEMENT ---
        bool isMoving = inputDir.Length() > 0 && Velocity.Length() > 0;
        if (isMoving && !_wasMoving)
        {
            // Started moving - play walk sound
            if (SFXWalk != null)
            {
                SFXWalk.Play();
            }
        }
        else if (!isMoving && _wasMoving)
        {
            // Stopped moving - stop walk sound
            StopWalkSound();
        }
        _wasMoving = isMoving;

        // --- DETERMINE FACING DIRECTION ---
        if (inputDir.Length() > 0)
        {
            // Update facing based on dominant axis
            if (Mathf.Abs(inputDir.X) > Mathf.Abs(inputDir.Y))
            {
                _lastFacing = inputDir.X > 0 ? "right" : "left";
            }
            else
            {
                // Vertical wins ties (perfect diagonal)
                _lastFacing = inputDir.Y > 0 ? "down" : "up";
            }
        }

        // --- UPDATE ANIMATION AND RAYCAST ---
        PlayAnimation(inputDir.Length() > 0);
        UpdateRaycast();
    }

    // =========================================================================
    // ! ANIMATION !
    // =========================================================================

    /// <summary>
    /// Plays appropriate walk or idle animation based on movement state.
    /// </summary>
    /// <param name="isMoving">True to play walk animation, false for idle.</param>
    private void PlayAnimation(bool isMoving)
    {
        if (AnimatedSprite == null)
        {
            return;
        }
        
        string animToPlay = "";

        if (isMoving)
        {
            animToPlay = "walk_" + _lastFacing;
        }
        else 
        {
            animToPlay = "idle_" + _lastFacing;
        }

        // Play animation if it exists, otherwise use fallback
        if (AnimatedSprite.SpriteFrames.HasAnimation(animToPlay))
        {
            AnimatedSprite.Play(animToPlay);
        }
        else if (AnimatedSprite.SpriteFrames.HasAnimation("idle"))
        {
            AnimatedSprite.Play("idle");
        }
    }

    // =========================================================================
    // ! RAYCAST !
    // =========================================================================

    /// <summary>
    /// Updates raycast direction to match player's facing direction.
    /// Snaps to cardinal directions only (no diagonals).
    /// </summary>
    private void UpdateRaycast()
    {
        if (InteractionRay == null)
        {
            return;
        }

        // Map facing string to cardinal direction vector
        Vector2 cardinalDir = Vector2.Zero;

        switch (_lastFacing)
        {
            case "up":
                cardinalDir = Vector2.Up;
                break;
            case "down":
                cardinalDir = Vector2.Down;
                break;
            case "left":
                cardinalDir = Vector2.Left;
                break;
            case "right":
                cardinalDir = Vector2.Right;
                break;
            default:
                cardinalDir = Vector2.Down;
                break;
        }

        // Apply direction and force immediate update
        InteractionRay.TargetPosition = cardinalDir * 30.0f;
        InteractionRay.ForceRaycastUpdate();
    }
    
    // =========================================================================
    // ! AUDIO !
    // =========================================================================

    /// <summary>
    /// Stops the walking sound effect if playing.
    /// </summary>
    private void StopWalkSound()
    {
        if (SFXWalk != null && SFXWalk.Playing)
        {
            SFXWalk.Stop();
        }
    }

    // =========================================================================
    // ! INPUT HANDLING !
    // =========================================================================

    /// <summary>
    /// Handles unprocessed input for interaction (Z key).
    /// Detects Toy and Door via point query first, then raycast for distant objects.
    /// </summary>
    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && keyEvent.Keycode == Key.Z)
        {
            if (!IsFrozen)
            {
                GodotObject objectHit = null;
                
                // First: check for areas at player's position using point query
                var spaceState = GetWorld2D().DirectSpaceState;
                var pointParams = new PhysicsPointQueryParameters2D();
                pointParams.Position = GlobalPosition;
                pointParams.CollideWithAreas = true;
                pointParams.CollideWithBodies = false;
                
                var results = spaceState.IntersectPoint(pointParams, 32);
                if (results.Count > 0)
                {
                    float closestDist = float.MaxValue;
                    foreach (var result in results)
                    {
                        var collider = result["collider"].AsGodotObject();
                        if (collider is Toy || collider is Door)
                        {
                            var node = collider as Node2D;
                            float dist = GlobalPosition.DistanceTo(node.GlobalPosition);
                            if (dist < closestDist)
                            {
                                closestDist = dist;
                                objectHit = collider;
                            }
                        }
                    }
                }
                
                // Fallback: try raycast for directional interaction at range
                if (objectHit == null && InteractionRay != null)
                {
                    InteractionRay.ForceRaycastUpdate();
                    if (InteractionRay.IsColliding())
                    {
                        objectHit = InteractionRay.GetCollider();
                    }
                }
                
                // Process the interaction
                if (objectHit is Toy toy)
                {
                    toy.Interact();
                    GetViewport().SetInputAsHandled();
                }
                else if (objectHit is Door door)
                {
                    door.Interact();
                    GetViewport().SetInputAsHandled();
                }
            }
        }
    }
}