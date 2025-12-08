using Godot;

// ==============================================================================
// ! DOOR.CS ! - Exit Door Entity
// ==============================================================================
// PURPOSE: Simple Area2D that emits a signal when the player interacts with it.
//          FactoryManager listens for this signal to show the exit prompt.
//
// DEBUGGING TIPS:
//   - Verify Area2D collision layer is set correctly
//   - Ensure Player's RayCast2D has CollideWithAreas = true
//   - Check FactoryManager has connected to DoorInteracted signal
// ==============================================================================

/// <summary>
/// Door: Exit door entity that emits interaction signal.
/// </summary>
public partial class Door : Area2D
{
    // =========================================================================
    // ! SIGNALS !
    // =========================================================================
    
    /// <summary>
    /// Emitted when player interacts with the door (presses Z while facing it).
    /// FactoryManager listens to this to show the "Leave for the night?" prompt.
    /// </summary>
    [Signal] public delegate void DoorInteractedEventHandler();

    // =========================================================================
    // ! INTERACTION !
    // =========================================================================

    /// <summary>
    /// Called by Player when Z is pressed while facing this door.
    /// </summary>
    public void Interact()
    {
        EmitSignal(SignalName.DoorInteracted);
    }
}