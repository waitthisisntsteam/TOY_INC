using Godot;
using System;

// ==============================================================================
// ! FACTORYMANAGER.CS - Main Game Controller !
// ==============================================================================
// PURPOSE: Manages the factory inspection game including:
//          - Shift timing (12AM-6AM over 3 minutes)
//          - Toy spawning and queue management
//          - Player interaction and dialogue
//          - Export/Discard mechanics
//          - Door exit functionality
//          - Audio management
//
// DEBUGGING TIPS:
//   - Check _currentShiftIndex for which shift is active (0-based)
//   - Check _isJammed if toys stop spawning
//   - Check _isChoosing/_isDoorChoosing for input state
//   - Check _activeData != null to verify shift is running
//   - All null checks use explicit braces for breakpoint placement
// ==============================================================================

/// <summary>
/// FactoryManager: Main game controller for the toy inspection factory game.
/// Handles shift management, toy spawning, player interaction, and UI.
/// </summary>
public partial class FactoryManager : Node2D
{
    // =========================================================================
    // ! CONSTANTS !
    // =========================================================================
    // NOTE: Modify these to adjust game pacing
    private const float SHIFT_DURATION = 180f;      // Total shift time in seconds (3 minutes)
    private const float SPAWN_INTERVAL = 10f;       // Time between toy spawns in seconds
    private const float QUEUE_OFFSET = 50f;         // Pixel offset between queued toys
    private const float TYPEWRITER_SPEED = 0.03f;   // Delay between characters in dialogue

    // =========================================================================
    // ! EXPORTED NODES ! - Assigned in Godot Editor
    // =========================================================================
    
    // --- Configuration ---
    [Export] public ShiftData[] AllShifts;          // Array of shift data resources
    
    // --- Core Nodes ---
    [Export] public Player PlayerCharacter;         // Reference to the player
    [Export] public Marker2D SpawnPoint;            // Where toys spawn (right side)
    [Export] public Marker2D InspectPoint;          // Where toys stop for inspection
    [Export] public RichTextLabel DialogueLabel;    // BBCode-enabled dialogue text
    [Export] public Label ClockLabel;               // Displays current time (12AM-6AM)
    [Export] public Control SpeakSpellPanel;        // Dialogue box container
    [Export] public CanvasModulate DarknessOverlay; // Screen tint for atmosphere/fades
    
    // --- UI Choice Labels ---
    [Export] public Label ExportLabel;              // "EXPORT" option display
    [Export] public Label DiscardLabel;             // "DISCARD" option display
    
    // --- Progress Bars ---
    [Export] public ProgressBar MeaningBar;         // Player meaning stat
    [Export] public ProgressBar SanityBar;          // Player sanity stat
    
    // --- Door ---
    [Export] public Door ExitDoor;                  // Exit door for shift end
    
    // --- Audio Players ---
    [Export] public AudioStreamPlayer SFXInteraction;   // Toy interaction sound
    [Export] public AudioStreamPlayer SFXExport;        // Export confirmation sound
    [Export] public AudioStreamPlayer SFXDiscard;       // Discard confirmation sound
    [Export] public AudioStreamPlayer SFXDoor;          // Door open sound
    [Export] public AudioStreamPlayer SFXCelebration;   // Shift complete fanfare
    [Export] public AudioStreamPlayer SFXToySpawn;      // New toy spawn sound
    [Export] public AudioStreamPlayer SFXJammed;        // Conveyor jam warning
    [Export] public AudioStreamPlayer SFXConveyor;      // Conveyor belt ambience
    [Export] public AudioStreamPlayer SFXMusic;         // Background music
    [Export] public AudioStreamPlayer SFXHover;         // Menu selection hover
    [Export] public AudioStreamPlayer SFXCancel;        // Cancel/back sound
    [Export] public AudioStreamPlayer SFXDialogue;      // Typewriter dialogue sound

    // =========================================================================
    // ! PRIVATE STATE VARIABLES ! - Game runtime state
    // =========================================================================
    
    // --- Toy Management ---
    private Toy _currentToy;                // Currently interacted toy (null if none)
    private Toy _toyAtSpawn;                // Last toy spawned (for jam detection)
    private int _toySpawnCount = 0;         // Total toys spawned this shift
    private int _toysInQueue = 0;           // Number of toys waiting in queue
    
    // --- Shift Management ---
    private ShiftData _activeData;          // Current shift configuration (null = shift ended)
    private int _currentShiftIndex = 0;     // Which shift we're on (0-based)
    private float _shiftElapsedTime = 0f;   // Seconds elapsed in current shift
    private float _timeSinceLastSpawn = 0f; // Timer for spawn interval
    private bool _isJammed = false;         // True if conveyor is jammed
    
    // --- Player State ---
    private Vector2 _playerSpawnPosition;   // Saved position for reset
    private float _originalMusicVolume = 0f;// Original music volume for fades
    
    // --- UI Choice State ---
    private bool _isChoosing = false;       // True when toy export/discard choice active
    private bool _isSelectedExport = true;  // True = Export selected, False = Discard
    private bool _isDoorChoosing = false;   // True when door yes/no choice active
    private bool _doorSelectedYes = true;   // True = Yes selected, False = No

    // --- UI Colors ---
    private Color _colorSelected = new Color(1, 1, 0);  // Yellow for selected option
    private Color _colorNormal = new Color(1, 1, 1);    // White for unselected
    
    // --- Typewriter Effect State ---
    private string _fullDialogueText = "";  // Complete dialogue text with BBCode
    private int _currentCharIndex = 0;      // Current visible character count
    private float _typewriterTimer = 0f;    // Timer for character reveal
    private bool _isTyping = false;         // True when typewriter effect is active

    // =========================================================================
    // ! LIFECYCLE METHODS !
    // =========================================================================

    /// <summary>
    /// Called when the node enters the scene tree.
    /// Initializes all game systems and starts the first shift.
    /// </summary>
    public override void _Ready()
    {
        // Connect door interaction signal
        if (ExitDoor != null)
        {
            ExitDoor.DoorInteracted += OnDoorInteracted;
        }

        // Store player spawn position for reset after shifts
        if (PlayerCharacter != null)
        {
            _playerSpawnPosition = PlayerCharacter.GlobalPosition;
        }
        
        // Store original music volume from editor for fade effects
        if (SFXMusic != null)
        {
            _originalMusicVolume = SFXMusic.VolumeDb;
        }

        // Initialize progress bars to starting values
        if (MeaningBar != null)
        {
            MeaningBar.Value = 0;
        }
        if (SanityBar != null)
        {
            SanityBar.Value = 100;
        }
        
        // Hide dialogue UI on startup
        HideDialogue();
        
        // Start background music immediately
        if (SFXMusic != null)
        {
            SFXMusic.Play();
        }

        // Begin first shift if shifts are configured
        if (AllShifts != null && AllShifts.Length > 0)
        {
            StartShift(0);
        }
    }

    // =========================================================================
    // ! PROCESS LOOP ! - Called every frame
    // =========================================================================

    /// <summary>
    /// Main game loop. Handles typewriter effect, time progression, and spawning.
    /// </summary>
    public override void _Process(double delta)
    {
        // --- TYPEWRITER EFFECT ---
        // Uses VisibleCharacters property to reveal text gradually
        // This properly handles BBCode tags without breaking formatting
        if (_isTyping && DialogueLabel != null)
        {
            _typewriterTimer += (float)delta;
            if (_typewriterTimer >= TYPEWRITER_SPEED)
            {
                _typewriterTimer = 0f;
                _currentCharIndex++;
                DialogueLabel.VisibleCharacters = _currentCharIndex;
                
                // Play dialogue sound for each character (skips last character)
                if (_currentCharIndex < DialogueLabel.GetTotalCharacterCount())
                {
                    if (SFXDialogue != null)
                    {
                        SFXDialogue.Play();
                    }
                }
                
                // Stop typing when all characters are revealed
                if (_currentCharIndex >= DialogueLabel.GetTotalCharacterCount())
                {
                    _isTyping = false;
                }
            }
        }
        
        // --- TIME AND SPAWN PROGRESSION ---
        // Only runs when a shift is active (_activeData != null)
        if (_activeData != null)
        {
            _shiftElapsedTime += (float)delta;
            UpdateClock();
            
            // Check if shift ended (6 AM reached)
            if (_shiftElapsedTime >= SHIFT_DURATION)
            {
                OnShiftEnd();
                return;
            }
            
            // Toy spawning logic (every 10 seconds, unless jammed)
            _timeSinceLastSpawn += (float)delta;
            if (_timeSinceLastSpawn >= SPAWN_INTERVAL && !_isJammed)
            {
                SpawnToy();
                _timeSinceLastSpawn = 0f;
            }
        }
    }

    // =========================================================================
    // ! UTILITY METHODS !
    // =========================================================================

    /// <summary>
    /// Removes all toy nodes from the scene.
    /// Called during shift transitions.
    /// </summary>
    private void ClearAllToys()
    {
        foreach (Node child in GetChildren())
        {
            if (child is Toy toy)
            {
                toy.QueueFree();
            }
        }
    }

    // =========================================================================
    // ! INPUT HANDLING !
    // =========================================================================

    /// <summary>
    /// Handles unprocessed input events for menu navigation and confirmations.
    /// Processes door choices and toy export/discard selections.
    /// </summary>
    public override void _UnhandledInput(InputEvent @event)
    {
        // Only process key press events
        if (@event is InputEventKey keyEvent && keyEvent.Pressed)
        {
            // --- DOOR CHOICE INPUT ---
            // Active when player interacts with exit door
            if (_isDoorChoosing)
            {
                // Navigate left/right between Yes/No
                if (@event.IsActionPressed("ui_left") && !_doorSelectedYes)
                {
                    _doorSelectedYes = true;
                    UpdateDoorUIColors();
                    if (SFXHover != null)
                    {
                        SFXHover.Play();
                    }
                    GetViewport().SetInputAsHandled();
                }
                else if (@event.IsActionPressed("ui_right") && _doorSelectedYes)
                {
                    _doorSelectedYes = false;
                    UpdateDoorUIColors();
                    if (SFXHover != null)
                    {
                        SFXHover.Play();
                    }
                    GetViewport().SetInputAsHandled();
                }
                
                // Confirm selection with Z or Enter
                if (keyEvent.Keycode == Key.Z || keyEvent.Keycode == Key.Enter)
                {
                    if (_doorSelectedYes)
                    {
                        EndShiftWithFade();
                    }
                    else
                    {
                        CancelDoorInteraction();
                    }
                    GetViewport().SetInputAsHandled();
                }
                // Cancel with X
                else if (keyEvent.Keycode == Key.X)
                {
                    CancelDoorInteraction();
                    GetViewport().SetInputAsHandled();
                }
                return;
            }
            
            // --- TOY INTERACTION INPUT ---
            // Active when player is viewing/choosing on a toy
            if (_currentToy != null && IsInstanceValid(_currentToy))
            {
                if (_isChoosing)
                {
                    // Front toy: can navigate between Export/Discard
                    if (@event.IsActionPressed("ui_left") && !_isSelectedExport)
                    {
                        _isSelectedExport = true;
                        UpdateUIColors();
                        if (SFXHover != null)
                        {
                            SFXHover.Play();
                        }
                        GetViewport().SetInputAsHandled();
                    }
                    else if (@event.IsActionPressed("ui_right") && _isSelectedExport)
                    {
                        _isSelectedExport = false;
                        UpdateUIColors();
                        if (SFXHover != null)
                        {
                            SFXHover.Play();
                        }
                        GetViewport().SetInputAsHandled();
                    }
                    
                    // Confirm choice with Z or Enter
                    if (keyEvent.Keycode == Key.Z || keyEvent.Keycode == Key.Enter)
                    {
                        ConfirmChoice();
                        GetViewport().SetInputAsHandled();
                    }
                    // Cancel with X
                    else if (keyEvent.Keycode == Key.X)
                    {
                        CancelInteraction();
                        GetViewport().SetInputAsHandled();
                    }
                }
                else
                {
                    // Blocked toy: only close dialogue, no choices
                    if (keyEvent.Keycode == Key.X || keyEvent.Keycode == Key.Z)
                    {
                        CloseMenu();
                        GetViewport().SetInputAsHandled();
                    }
                }
            }
        }
    }

    // =========================================================================
    // ! CHOICE CONFIRMATION METHODS !
    // =========================================================================

    /// <summary>
    /// Confirms the player's export/discard choice for the current toy.
    /// Updates bars, plays sounds, and manages queue.
    /// </summary>
    private void ConfirmChoice()
    {
        // 1. Dismiss toy with animation and play appropriate sound
        _currentToy.Dismiss(_isSelectedExport);
        if (_isSelectedExport)
        {
            if (SFXExport != null)
            {
                SFXExport.Play();
            }
        }
        else
        {
            if (SFXDiscard != null)
            {
                SFXDiscard.Play();
            }
        }
        
        // 2. Update progress bars (fixed +2.5 meaning, -2.5 sanity per toy)
        if (MeaningBar != null)
        {
            MeaningBar.Value = Mathf.Min(MeaningBar.Value + 2.5f, MeaningBar.MaxValue);
        }
        if (SanityBar != null)
        {
            SanityBar.Value = Mathf.Max(SanityBar.Value - 2.5f, 0);
        }
        
        // 3. Clear jam state if dismissed toy was blocking spawn
        if (_currentToy == _toyAtSpawn)
        {
            _toyAtSpawn = null;
            _isJammed = false;
        }
        
        // 4. Shift remaining toys forward by 50 pixels
        _toysInQueue = Mathf.Max(0, _toysInQueue - 1);
        ShiftToysForward();

        // 5. Close the menu UI
        CloseMenu();
    }
    
    /// <summary>
    /// Advances all waiting toys' target positions forward.
    /// Called after a toy is dismissed to fill the gap.
    /// </summary>
    private void ShiftToysForward()
    {
        // Move all waiting toys' TargetX forward by QUEUE_OFFSET (50 pixels)
        foreach (Node child in GetChildren())
        {
            if (child is Toy toy && toy != _currentToy && IsInstanceValid(toy))
            {
                // Only shift toys that haven't been dismissed (TargetX < 99999)
                if (toy.TargetX < 99999f)
                {
                    toy.TargetX += QUEUE_OFFSET;
                }
            }
        }
    }
    
    /// <summary>
    /// Finds the toy at the front of the queue (highest X position).
    /// Used to determine which toy can be exported/discarded.
    /// </summary>
    /// <returns>The frontmost toy, or null if no valid toys exist.</returns>
    private Toy GetFrontMostToy()
    {
        Toy frontToy = null;
        float highestX = float.MinValue;
        
        foreach (Node child in GetChildren())
        {
            if (child is Toy toy && IsInstanceValid(toy) && toy.TargetX < 99999f)
            {
                if (toy.GlobalPosition.X > highestX)
                {
                    highestX = toy.GlobalPosition.X;
                    frontToy = toy;
                }
            }
        }
        
        return frontToy;
    }
    
    // =========================================================================
    // ! TIME MANAGEMENT !
    // =========================================================================

    /// <summary>
    /// Updates the clock display based on elapsed shift time.
    /// Maps 0-180 seconds to 12AM-6AM display.
    /// </summary>
    private void UpdateClock()
    {
        // Guard clause for null reference
        if (ClockLabel == null)
        {
            return;
        }

        // Calculate progress through shift (0.0 to 1.0)
        float progress = _shiftElapsedTime / SHIFT_DURATION;
        progress = Mathf.Clamp(progress, 0f, 1f);
        
        // Map progress to hours (0 to 6 hours passed)
        // Start at 12AM (hour 12), end at 6AM (hour 6)
        double hoursPassed = progress * 6.0;
        
        // Convert to display hour (12, 1, 2, 3, 4, 5, 6)
        int currentHour = 12 + (int)hoursPassed;
        if (currentHour > 12)
        {
            currentHour -= 12; // 13->1, 14->2, etc.
        }
        
        // Update display
        ClockLabel.Text = $"{currentHour} AM";
    }

    // =========================================================================
    // ! MENU INTERACTION METHODS !
    // =========================================================================

    /// <summary>
    /// Cancels toy interaction and plays cancel sound.
    /// </summary>
    private void CancelInteraction()
    {
        if (SFXCancel != null)
        {
            SFXCancel.Play();
        }
        CloseMenu();
    }

    /// <summary>
    /// Closes the toy interaction menu and unfreezes player.
    /// </summary>
    private void CloseMenu()
    {
        _currentToy = null;
        _isChoosing = false;
        if (PlayerCharacter != null)
        {
            PlayerCharacter.IsFrozen = false;
        }
        HideDialogue();
    }
    
    // =========================================================================
    // ! DOOR INTERACTION !
    // =========================================================================
    
    /// <summary>
    /// Called when player interacts with the exit door.
    /// Shows "Leave for the night?" dialogue with Yes/No choice.
    /// </summary>
    public void OnDoorInteracted()
    {
        // Guard: Don't allow door interaction during other choices
        if (_isChoosing || _isDoorChoosing)
        {
            return;
        }
        
        // Freeze player and enter door choice mode
        if (PlayerCharacter != null)
        {
            PlayerCharacter.IsFrozen = true;
        }
        _isDoorChoosing = true;
        _doorSelectedYes = true;
        
        // Play interaction sound
        if (SFXInteraction != null)
        {
            SFXInteraction.Play();
        }
        
        // Show door dialogue
        StartTypewriter("[center]Leave for the night?[/center]");
        if (SpeakSpellPanel != null)
        {
            SpeakSpellPanel.Visible = true;
        }
        if (ExportLabel != null)
        {
            ExportLabel.Visible = true;
        }
        if (DiscardLabel != null)
        {
            DiscardLabel.Visible = true;
        }
        
        UpdateDoorUIColors();
    }
    
    /// <summary>
    /// Updates Yes/No label colors for door choice.
    /// </summary>
    private void UpdateDoorUIColors()
    {
        if (ExportLabel == null || DiscardLabel == null)
        {
            return;
        }
        
        if (_doorSelectedYes)
        {
            ExportLabel.Modulate = _colorSelected;
            ExportLabel.Text = " YES<";
            DiscardLabel.Modulate = _colorNormal;
            DiscardLabel.Text = " NO ";
        }
        else
        {
            ExportLabel.Modulate = _colorNormal;
            ExportLabel.Text = " YES ";
            DiscardLabel.Modulate = _colorSelected;
            DiscardLabel.Text = ">NO ";
        }
    }
    
    /// <summary>
    /// Cancels door interaction and returns player to normal state.
    /// </summary>
    private void CancelDoorInteraction()
    {
        if (SFXCancel != null)
        {
            SFXCancel.Play();
        }
        _isDoorChoosing = false;
        if (PlayerCharacter != null)
        {
            PlayerCharacter.IsFrozen = false;
        }
        HideDialogue();
    }
    
    // =========================================================================
    // ! SHIFT TRANSITION METHODS !
    // =========================================================================

    /// <summary>
    /// Handles voluntary shift end when player exits through door.
    /// Fades screen to black, plays sounds, transitions to next shift.
    /// </summary>
    private async void EndShiftWithFade()
    {
        _isDoorChoosing = false;
        
        // Play door sound
        if (SFXDoor != null)
        {
            SFXDoor.Play();
        }
        
        // Stop time progression by clearing active data
        var previousActiveData = _activeData;
        _activeData = null;
        
        // Hide choice UI
        HideDialogue();
        if (ExportLabel != null)
        {
            ExportLabel.Visible = false;
        }
        if (DiscardLabel != null)
        {
            DiscardLabel.Visible = false;
        }
        
        // Create fade-out tween for screen, UI, and music
        var tween = CreateTween();
        tween.SetParallel(true);
        
        if (DarknessOverlay != null)
        {
            tween.TweenProperty(DarknessOverlay, "color", new Color(0, 0, 0, 1), 1.0f);
        }
        if (ClockLabel != null)
        {
            tween.TweenProperty(ClockLabel, "modulate", new Color(1, 1, 1, 0), 1.0f);
        }
        if (MeaningBar != null)
        {
            tween.TweenProperty(MeaningBar, "modulate", new Color(1, 1, 1, 0), 1.0f);
        }
        if (SanityBar != null)
        {
            tween.TweenProperty(SanityBar, "modulate", new Color(1, 1, 1, 0), 1.0f);
        }
        if (SFXMusic != null)
        {
            tween.TweenProperty(SFXMusic, "volume_db", -80.0f, 1.0f);
        }
        
        // Wait for fade to complete
        await ToSignal(tween, Tween.SignalName.Finished);
        
        // Stop music after fade completes
        if (SFXMusic != null)
        {
            SFXMusic.Stop();
        }
        
        // Play celebration sound and wait for it to finish
        if (SFXCelebration != null)
        {
            SFXCelebration.Play();
            await ToSignal(SFXCelebration, AudioStreamPlayer.SignalName.Finished);
        }
        
        // Show shift completion text
        if (DialogueLabel != null)
        {
            DialogueLabel.Text = $"[center]Shift {_currentShiftIndex + 1} Over[/center]";
        }
        if (SpeakSpellPanel != null)
        {
            SpeakSpellPanel.Visible = true;
        }
        
        // Wait 2 seconds before transition
        await ToSignal(GetTree().CreateTimer(2.0f), SceneTreeTimer.SignalName.Timeout);
        
        // Clean up current shift
        HideDialogue();
        ClearAllToys();
        
        // Reset player to spawn position
        if (PlayerCharacter != null)
        {
            PlayerCharacter.GlobalPosition = _playerSpawnPosition;
            PlayerCharacter.IsFrozen = false;
            PlayerCharacter.ResetToIdleDown();
        }
        
        // Start next shift or show completion message
        int nextShift = _currentShiftIndex + 1;
        if (AllShifts != null && nextShift < AllShifts.Length)
        {
            StartShift(nextShift);
        }
        else
        {
            // No more shifts - show completion message
            if (DialogueLabel != null)
            {
                DialogueLabel.Text = "[center]Shift 3 to be added...\n\nThanks for playing![/center]";
            }
            if (SpeakSpellPanel != null)
            {
                SpeakSpellPanel.Visible = true;
            }
        }
    }

    // =========================================================================
    // ! TOY INTERACTION HANDLER !
    // =========================================================================

    /// <summary>
    /// Called when player interacts with a toy via signal.
    /// Determines if toy is at front of queue (can export/discard) or blocked (view only).
    /// </summary>
    /// <param name="toy">The toy being interacted with.</param>
    /// <param name="thought">The dialogue text to display.</param>
    public void OnToyInteracted(Toy toy, string thought)
    {
        // Determine if this toy is at the front of the queue
        Toy frontToy = GetFrontMostToy();
        bool isFrontToy = (frontToy != null && toy == frontToy);
        
        _currentToy = toy;
        
        // Freeze player during interaction
        if (PlayerCharacter != null)
        {
            PlayerCharacter.IsFrozen = true;
        }
        
        // Play interaction sound
        if (SFXInteraction != null)
        {
            SFXInteraction.Play();
        }
        
        if (isFrontToy)
        {
            // Front toy: enable export/discard choice
            _isChoosing = true; 
            _isSelectedExport = true;
            ShowDialogue(thought);
        }
        else
        {
            // Blocked toy: show message only, no choices
            _isChoosing = false;
            ShowDialogueOnly(thought);
        }
    }
    
    /// <summary>
    /// Shows dialogue without export/discard options.
    /// Used for blocked toys that can't be processed yet.
    /// </summary>
    /// <param name="text">The dialogue text to display.</param>
    private void ShowDialogueOnly(string text)
    {
        StartTypewriter($"[shake rate=10 level=3]{text}[/shake]");
        if (SpeakSpellPanel != null)
        {
            SpeakSpellPanel.Visible = true;
        }
        if (ExportLabel != null)
        {
            ExportLabel.Visible = false;
        }
        if (DiscardLabel != null)
        {
            DiscardLabel.Visible = false;
        }
    }
    
    // =========================================================================
    // ! SHIFT MANAGEMENT !
    // =========================================================================

    /// <summary>
    /// Initializes and starts a new shift.
    /// Resets all counters, timers, and UI to starting state.
    /// </summary>
    /// <param name="index">Zero-based index of the shift to start.</param>
    private void StartShift(int index)
    {
        // Guard clause for invalid shift index
        if (AllShifts == null || index >= AllShifts.Length)
        {
            return;
        }
        
        _currentShiftIndex = index;
        _activeData = AllShifts[index];
        
        // Reset spawn counter
        _toySpawnCount = 0;
        
        // Reset time tracking
        _shiftElapsedTime = 0f;
        _timeSinceLastSpawn = 0f;
        _isJammed = false;
        _toyAtSpawn = null;
        _toysInQueue = 0;
        
        // Reset clock display to 12 AM
        if (ClockLabel != null)
        {
            ClockLabel.Text = "12 AM";
        }
        
        // Reset UI visibility (may have been faded from previous shift)
        if (ClockLabel != null)
        {
            ClockLabel.Modulate = new Color(1, 1, 1, 1);
        }
        if (MeaningBar != null)
        {
            MeaningBar.Modulate = new Color(1, 1, 1, 1);
        }
        if (SanityBar != null)
        {
            SanityBar.Modulate = new Color(1, 1, 1, 1);
        }
        
        // Reset music to original volume and start playing
        if (SFXMusic != null)
        {
            SFXMusic.VolumeDb = _originalMusicVolume;
            SFXMusic.Play();
        }

        // Spawn first toy immediately
        SpawnToy();

        // Set atmosphere color for this shift
        if (DarknessOverlay != null)
        {
            DarknessOverlay.Color = _activeData.AtmosphereColor;
        }
        
        // Show shift intro text
        StartTypewriter($"[shake rate=10 level=3]{_activeData.IntroText}[/shake]");
        if (SpeakSpellPanel != null)
        {
            SpeakSpellPanel.Visible = true;
        }
        if (ExportLabel != null)
        {
            ExportLabel.Visible = false;
        }
        if (DiscardLabel != null)
        {
            DiscardLabel.Visible = false;
        }
    }

    // =========================================================================
    // ! TOY SPAWNING !
    // =========================================================================

    /// <summary>
    /// Spawns a new toy at the spawn point.
    /// Handles jam detection if previous toy hasn't moved.
    /// </summary>
    private void SpawnToy()
    {
        // Guard clause for missing data
        if (_activeData == null || _activeData.ToyScene == null)
        {
            return;
        }
        
        // Check if there's already a toy at or near the spawn point (jam detection)
        if (_toyAtSpawn != null && IsInstanceValid(_toyAtSpawn))
        {
            float distanceFromSpawn = _toyAtSpawn.GlobalPosition.DistanceTo(SpawnPoint.GlobalPosition);
            // Within 50 pixels of spawn = conveyor is jammed
            if (distanceFromSpawn < 50f)
            {
                _isJammed = true;
                if (SFXJammed != null)
                {
                    SFXJammed.Play();
                }
                ShowJamDialogue();
                return;
            }
        }
        
        // Instantiate new toy from scene
        var newToy = _activeData.ToyScene.Instantiate<Toy>();
        AddChild(newToy);
        
        // Play spawn sound
        if (SFXToySpawn != null)
        {
            SFXToySpawn.Play();
        }
        
        // Start conveyor sound if not already playing
        if (SFXConveyor != null && !SFXConveyor.Playing)
        {
            SFXConveyor.Play();
        }
        
        // Position toy at spawn point
        if (SpawnPoint != null)
        {
            newToy.GlobalPosition = SpawnPoint.GlobalPosition;
        }
        
        // Calculate target X position with queue offset
        // Each toy in queue pushes this one back by QUEUE_OFFSET pixels
        if (InspectPoint != null) 
        {
            newToy.TargetX = InspectPoint.GlobalPosition.X - (_toysInQueue * QUEUE_OFFSET);
        }
        _toysInQueue++;

        // Connect signals for interaction and stopping
        newToy.ToyInteracted += OnToyInteracted;
        newToy.ToyStopped += OnToyStopped;
        
        // Track this as the most recent spawned toy (for jam detection)
        _toyAtSpawn = newToy;
        
        // Assign ToyKey based on shift rules (determines good/evil dialogue)
        int toyNumber = _toySpawnCount + 1; // 1-based for readability
        newToy.ToyKey = GetToyKeyForShift(_currentShiftIndex, toyNumber);
        
        // Set dialogue based on key: even = good, odd = evil
        if (newToy.ToyKey % 2 == 0)
        {
            newToy.CurrentThought = "im good";
        }
        else
        {
            newToy.CurrentThought = "im evil";
        }
        
        _toySpawnCount++;
    }
    
    /// <summary>
    /// Called when a toy reaches its target position.
    /// Stops the conveyor belt sound.
    /// </summary>
    private void OnToyStopped()
    {
        // Stop conveyor sound when toy reaches its target
        if (SFXConveyor != null && SFXConveyor.Playing)
        {
            SFXConveyor.Stop();
        }
    }

    // =========================================================================
    // ! TOY KEY ASSIGNMENT ! (Determines good/evil per shift)
    // =========================================================================

    /// <summary>
    /// Determines the ToyKey for a toy based on shift and spawn order.
    /// Even keys = good toys, Odd keys = evil toys.
    /// </summary>
    /// <param name="shiftIndex">Current shift (0-based).</param>
    /// <param name="toyNumber">Toy number in spawn order (1-based).</param>
    /// <returns>ToyKey: even for good, odd for evil.</returns>
    private int GetToyKeyForShift(int shiftIndex, int toyNumber)
    {
        // Shift 0 (first shift): Only the 3rd toy is evil
        if (shiftIndex == 0)
        {
            if (toyNumber == 3)
            {
                return 3; // Odd = evil
            }
            else
            {
                return 2; // Even = good
            }
        }
        // Shift 1 (second shift): Alternating - odd-numbered toys are evil
        else if (shiftIndex == 1)
        {
            if (toyNumber % 2 == 1)
            {
                return 1; // Odd = evil
            }
            else
            {
                return 2; // Even = good
            }
        }
        // Shift 2+ (third shift and beyond): All good (placeholder for future)
        else
        {
            return 2; // Even = good (default)
        }
    }

    // =========================================================================
    // ! UI HELPER METHODS !
    // =========================================================================
    
    /// <summary>
    /// Shows dialogue with export/discard options.
    /// </summary>
    /// <param name="text">The dialogue text to display.</param>
    private void ShowDialogue(string text)
    {
        StartTypewriter($"[shake rate=10 level=3]{text}[/shake]");
        if (SpeakSpellPanel != null)
        {
            SpeakSpellPanel.Visible = true;
        }
        if (ExportLabel != null)
        {
            ExportLabel.Visible = true;
        }
        if (DiscardLabel != null)
        {
            DiscardLabel.Visible = true;
        }
        UpdateUIColors();
    }

    /// <summary>
    /// Hides all dialogue UI elements.
    /// </summary>
    private void HideDialogue()
    {
        if (DialogueLabel != null)
        {
            DialogueLabel.Text = "";
            DialogueLabel.VisibleCharacters = -1; // Reset to show all
        }
        if (SpeakSpellPanel != null)
        {
            SpeakSpellPanel.Visible = false;
        }
        _isTyping = false;
    }
    
    /// <summary>
    /// Starts the typewriter effect for dialogue text.
    /// Sets up state for _Process to animate character reveal.
    /// </summary>
    /// <param name="text">Full BBCode text to display gradually.</param>
    private void StartTypewriter(string text)
    {
        _fullDialogueText = text;
        _currentCharIndex = 0;
        _typewriterTimer = 0f;
        _isTyping = true;
        
        if (DialogueLabel != null)
        {
            DialogueLabel.Text = text;
            DialogueLabel.VisibleCharacters = 0;
        }
    }
    
    /// <summary>
    /// Shows jam dialogue when conveyor is blocked.
    /// </summary>
    private void ShowJamDialogue()
    {
        StartTypewriter("It's jammed.");
        if (SpeakSpellPanel != null)
        {
            SpeakSpellPanel.Visible = true;
        }
        if (ExportLabel != null)
        {
            ExportLabel.Visible = false;
        }
        if (DiscardLabel != null)
        {
            DiscardLabel.Visible = false;
        }
    }

    /// <summary>
    /// Updates Export/Discard label colors based on current selection.
    /// </summary>
    private void UpdateUIColors()
    {
        if (ExportLabel == null || DiscardLabel == null)
        {
            return;
        }
        
        if (_isSelectedExport)
        {
            ExportLabel.Modulate = _colorSelected;
            ExportLabel.Text = " EXPORT<";
            DiscardLabel.Modulate = _colorNormal;
            DiscardLabel.Text = " DISCARD ";
        }
        else
        {
            ExportLabel.Modulate = _colorNormal;
            ExportLabel.Text = " EXPORT ";
            DiscardLabel.Modulate = _colorSelected;
            DiscardLabel.Text = ">DISCARD ";
        }
    }

    // =========================================================================
    // ! AUTOMATIC SHIFT END (Time-based) !
    // =========================================================================

    /// <summary>
    /// Called when shift time expires (6 AM reached).
    /// Handles automatic shift transition with fade effects.
    /// </summary>
    private async void OnShiftEnd()
    {
        // Stop time progression
        _activeData = null;
        
        // Freeze player during transition
        if (PlayerCharacter != null)
        {
            PlayerCharacter.IsFrozen = true;
        }
        
        // Hide dialogue and choice UI
        HideDialogue();
        if (ExportLabel != null)
        {
            ExportLabel.Visible = false;
        }
        if (DiscardLabel != null)
        {
            DiscardLabel.Visible = false;
        }
        
        // Create fade-out tween for screen, UI, and music
        var tween = CreateTween();
        tween.SetParallel(true);
        
        if (DarknessOverlay != null)
        {
            tween.TweenProperty(DarknessOverlay, "color", new Color(0, 0, 0, 1), 1.0f);
        }
        if (ClockLabel != null)
        {
            tween.TweenProperty(ClockLabel, "modulate", new Color(1, 1, 1, 0), 1.0f);
        }
        if (MeaningBar != null)
        {
            tween.TweenProperty(MeaningBar, "modulate", new Color(1, 1, 1, 0), 1.0f);
        }
        if (SanityBar != null)
        {
            tween.TweenProperty(SanityBar, "modulate", new Color(1, 1, 1, 0), 1.0f);
        }
        if (SFXMusic != null)
        {
            tween.TweenProperty(SFXMusic, "volume_db", -80.0f, 1.0f);
        }
        
        // Wait for fade to complete
        await ToSignal(tween, Tween.SignalName.Finished);
        
        // Stop music after fade
        if (SFXMusic != null)
        {
            SFXMusic.Stop();
        }
        
        // Play celebration sound and wait for completion
        if (SFXCelebration != null)
        {
            SFXCelebration.Play();
            await ToSignal(SFXCelebration, AudioStreamPlayer.SignalName.Finished);
        }
        
        // Show shift completion text
        if (DialogueLabel != null)
        {
            DialogueLabel.Text = $"[center]Shift {_currentShiftIndex + 1} Over[/center]";
        }
        if (SpeakSpellPanel != null)
        {
            SpeakSpellPanel.Visible = true;
        }
        
        // Wait 2 seconds before transition
        await ToSignal(GetTree().CreateTimer(2.0f), SceneTreeTimer.SignalName.Timeout);
        
        // Clean up current shift
        HideDialogue();
        ClearAllToys();
        
        // Reset player to spawn position
        if (PlayerCharacter != null)
        {
            PlayerCharacter.GlobalPosition = _playerSpawnPosition;
            PlayerCharacter.IsFrozen = false;
            PlayerCharacter.ResetToIdleDown();
        }
        
        // Start next shift or show completion message
        int nextShift = _currentShiftIndex + 1;
        if (AllShifts != null && nextShift < AllShifts.Length)
        {
            StartShift(nextShift);
        }
        else
        {
            // No more shifts - show completion message
            if (DialogueLabel != null)
            {
                DialogueLabel.Text = "[center]Shift 3 to be added...\n\nThanks for playing![/center]";
            }
            if (SpeakSpellPanel != null)
            {
                SpeakSpellPanel.Visible = true;
            }
        }
    }
}