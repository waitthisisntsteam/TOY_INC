using Godot;
using Godot.Collections;

// ==============================================================================
// ! SHIFTDATA.CS ! - Shift Configuration Resource
// ==============================================================================
// PURPOSE: Godot Resource that holds all configuration for a single shift.
//          Created via Create New... menu in Godot Editor.
//          Assigned to FactoryManager's AllShifts array.
//
// USAGE:
//   1. Right-click in FileSystem > Create New > Resource
//   2. Select ShiftData
//   3. Configure properties in Inspector
//   4. Drag into FactoryManager's AllShifts array
//
// DEBUGGING TIPS:
//   - Verify ToyScene is assigned (required for toy spawning)
//   - Check AtmosphereColor for visual issues
//   - IntroText uses BBCode for formatting
// ==============================================================================

/// <summary>
/// ShiftData: Resource defining configuration for a single shift.
/// </summary>
[GlobalClass] // Makes "ShiftData" appear in Create New... menu
public partial class ShiftData : Resource
{
    // =========================================================================
    // ! GENERAL SETTINGS !
    // =========================================================================
    
    [ExportGroup("Settings")]
    
    /// <summary>Display name for the shift (e.g., "Shift 1").</summary>
    [Export] public string ShiftName { get; set; } = "Shift 1";
    
    /// <summary>Duration of the shift in seconds (default: 60 for testing).</summary>
    [Export] public float DurationSeconds { get; set; } = 60.0f;
    
    /// <summary>Color tint applied to screen via CanvasModulate.</summary>
    [Export] public Color AtmosphereColor { get; set; } = new Color(0.2f, 0.2f, 0.2f);

    // =========================================================================
    // ! TOY CONFIGURATION !
    // =========================================================================
    
    [ExportGroup("Toy Logic")]
    
    /// <summary>Seconds between toy spawns.</summary>
    [Export] public float SpawnInterval { get; set; } = 3.0f;
    
    /// <summary>The Toy scene to instantiate (Toy.tscn).</summary>
    [Export] public PackedScene ToyScene { get; set; } 

    // =========================================================================
    // ! NARRATIVE TEXT !
    // =========================================================================
    
    [ExportGroup("Narrative")]
    
    /// <summary>BBCode text shown at shift start.</summary>
    [Export(PropertyHint.MultilineText)] public string IntroText { get; set; }
    
    /// <summary>
    /// Special dialogue for specific toy numbers.
    /// Key = toy count (e.g., 4th toy), Value = "Custom dialogue text"
    /// </summary>
    [Export] public Dictionary<int, string> ScriptedThoughts { get; set; }
}
// ==============================================================================
// ! END OF SHIFTDATA.CS !
// ==============================================================================