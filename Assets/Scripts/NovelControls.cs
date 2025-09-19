// NovelControls.cs - Complete visual novel engine
using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class NovelControls : Control
{
    [Signal] public delegate void TextAdvancedEventHandler();
    [Signal] public delegate void ChoiceMadeEventHandler(int choiceIndex, string choiceText);
    [Signal] public delegate void NovelActivatedEventHandler();
    [Signal] public delegate void NovelDeactivatedEventHandler();
    [Signal] public delegate void DialogueCompletedEventHandler();
    
    [Export] private RichTextLabel textDisplay;
    [Export] private RichTextLabel historyDisplay;
    [Export] private Control backgroundContainer;
    [Export] private Control characterContainer;
    [Export] private Container choicesContainer;
    [Export] private float textSpeed = 50f; // Characters per second
    [Export] private bool autoAdvanceOnClick = true;
    [Export] private bool enableHistory = true;
    
    private bool isActive = false;
    private bool isShowingChoices = false;
    private bool isTextAnimating = false;
    private int currentChoiceIndex = 0;
    private List<Button> choiceButtons = new();
    
    // Character positioning
    private List<TextureRect> characterSprites = new();
    private const int MAX_EVEN_SPRITES = 4; // A, B, C, D
    private const int MAX_ODD_SPRITES = 5;  // A, B, C, D, E
    
    // Text animation
    private Tween textTween;
    private string currentFullText = "";
    private string dialogueHistory = "";
    
    public bool IsActive => isActive;
    public bool IsShowingChoices => isShowingChoices;
    public Vector2 CurrentActionPosition => GetCurrentCursorPosition();
    
    #region Public API
    
    public override void _Ready()
    {
        InitializeNovel();
    }
    
    public void SetActive(bool active)
    {
        if (isActive == active) return;
        
        isActive = active;
        Visible = active;
        
        if (active)
            ActivateNovel();
        else
            DeactivateNovel();
    }
    
    public void Navigate(Vector2I direction)
    {
        if (!isActive || !isShowingChoices) return;
        
        NavigateChoices(direction.Y);
    }
    
    public void AdvanceText()
    {
        if (!isActive) return;
        
        if (isTextAnimating)
        {
            CompleteTextAnimation();
        }
        else if (isShowingChoices)
        {
            // Can't advance while showing choices
            return;
        }
        else
        {
            // Ready for next text
            EmitSignal(SignalName.TextAdvanced);
        }
    }
    
    public void MakeCurrentChoice()
    {
        if (!isActive || !isShowingChoices || choiceButtons.Count == 0) return;
        
        var choiceText = choiceButtons[currentChoiceIndex].Text;
        MakeChoice(currentChoiceIndex, choiceText);
    }
    
    public void ShowText(string text, string speaker = "")
    {
        if (!isActive) return;
        
        PrepareTextDisplay(text, speaker);
        AnimateTextDisplay();
    }
    
    public void ShowChoices(string[] choices)
    {
        if (!isActive) return;
        
        CreateChoiceButtons(choices);
        ShowChoiceInterface();
    }
    
    public void SetBackground(Texture2D backgroundTexture)
    {
        SetBackgroundImage(backgroundTexture);
    }
    
    public void ShowCharacter(int position, Texture2D characterTexture)
    {
        SetCharacterSprite(position, characterTexture);
    }
    
    public void HideCharacter(int position)
    {
        ClearCharacterSprite(position);
    }
    
    #endregion
    
    #region Novel Management
    
    private void InitializeNovel()
    {
        FindComponents();
        SetupCharacterPositions();
        SetupTextDisplay();
        SetupChoicesContainer();
        SetupInitialState();
    }
    
    private void FindComponents()
    {
        textDisplay ??= GetNodeOrNull<RichTextLabel>("TextDisplay");
        historyDisplay ??= GetNodeOrNull<RichTextLabel>("HistoryDisplay");
        backgroundContainer ??= GetNodeOrNull<Control>("BackgroundContainer");
        characterContainer ??= GetNodeOrNull<Control>("CharacterContainer");
        choicesContainer ??= GetNodeOrNull<Container>("ChoicesContainer");
        
        if (textDisplay == null)
            GD.PrintErr($"[NovelControls] No text display found in {Name}");
    }
    
    private void SetupCharacterPositions()
    {
        if (characterContainer == null) return;
        
        // Create character sprite positions
        var maxSprites = Mathf.Max(MAX_EVEN_SPRITES, MAX_ODD_SPRITES);
        for (int i = 0; i < maxSprites; i++)
        {
            var sprite = new TextureRect();
            sprite.Visible = false;
            sprite.ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional;
            sprite.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            characterContainer.AddChild(sprite);
            characterSprites.Add(sprite);
        }
        
        PositionCharacterSprites();
    }
    
    private void SetupTextDisplay()
    {
        if (textDisplay == null) return;
        
        textDisplay.BbcodeEnabled = true;
        textDisplay.ScrollFollowing = true;
        textDisplay.FitContent = true;
    }
    
    private void SetupChoicesContainer()
    {
        if (choicesContainer == null) return;
        
        choicesContainer.Visible = false;
    }
    
    private void SetupInitialState()
    {
        isActive = false;
        isShowingChoices = false;
        isTextAnimating = false;
        currentChoiceIndex = 0;
        
        textTween = CreateTween();
        textTween.Kill();
    }
    
    private void ActivateNovel()
    {
        EmitSignal(SignalName.NovelActivated);
        GD.Print($"[NovelControls] Activated novel: {Name}");
    }
    
    private void DeactivateNovel()
    {
        StopTextAnimation();
        HideChoices();
        EmitSignal(SignalName.NovelDeactivated);
        GD.Print($"[NovelControls] Deactivated novel: {Name}");
    }
    
    #endregion
    
    #region Character Management
    
    private void PositionCharacterSprites()
    {
        if (characterContainer == null || characterSprites.Count == 0) return;
        
        var containerSize = characterContainer.Size;
        if (containerSize == Vector2.Zero) containerSize = GetViewportRect().Size;
        
        // Position sprites for both even and odd layouts
        for (int i = 0; i < characterSprites.Count; i++)
        {
            var sprite = characterSprites[i];
            PositionSpriteAtIndex(sprite, i, containerSize);
        }
    }
    
    private void PositionSpriteAtIndex(TextureRect sprite, int index, Vector2 containerSize)
    {
        Vector2 position;
        
        if (index < MAX_EVEN_SPRITES)
        {
            // Even sprites: A, B, C, D (4 equal parts)
            var width = containerSize.X / MAX_EVEN_SPRITES;
            position = new Vector2(width * index + width * 0.5f, containerSize.Y * 0.5f);
        }
        else
        {
            // Odd sprites: A, B, C, D, E (5 equal parts, in between even sprites)
            var evenIndex = index - MAX_EVEN_SPRITES;
            if (evenIndex < MAX_ODD_SPRITES)
            {
                var width = containerSize.X / MAX_ODD_SPRITES;
                position = new Vector2(width * evenIndex + width * 0.5f, containerSize.Y * 0.5f);
            }
            else
            {
                position = Vector2.Zero;
            }
        }
        
        sprite.Position = position - sprite.Size * 0.5f;
    }
    
    private void SetCharacterSprite(int position, Texture2D texture)
    {
        if (position < 0 || position >= characterSprites.Count) return;
        
        var sprite = characterSprites[position];
        sprite.Texture = texture;
        sprite.Visible = texture != null;
        
        GD.Print($"[NovelControls] Set character sprite at position {position}");
    }
    
    private void ClearCharacterSprite(int position)
    {
        if (position < 0 || position >= characterSprites.Count) return;
        
        var sprite = characterSprites[position];
        sprite.Texture = null;
        sprite.Visible = false;
    }
    
    private void SetBackgroundImage(Texture2D texture)
    {
        if (backgroundContainer == null) return;
        
        // Find or create background image
        var bgImage = backgroundContainer.GetNodeOrNull<TextureRect>("Background");
        if (bgImage == null)
        {
            bgImage = new TextureRect();
            bgImage.Name = "Background";
            bgImage.ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional;
            bgImage.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            backgroundContainer.AddChild(bgImage);
        }
        
        bgImage.Texture = texture;
    }
    
    #endregion
    
    #region Text Animation
    
    private void PrepareTextDisplay(string text, string speaker)
    {
        if (textDisplay == null) return;
        
        currentFullText = FormatDialogueText(text, speaker);
        AddToHistory(currentFullText);
    }
    
    private string FormatDialogueText(string text, string speaker)
    {
        if (string.IsNullOrEmpty(speaker))
            return text;
        
        return $"[b]{speaker}[/b]\n{text}";
    }
    
    private void AnimateTextDisplay()
    {
        if (textDisplay == null || string.IsNullOrEmpty(currentFullText)) return;
        
        isTextAnimating = true;
        textDisplay.Text = "";
        
        // Animate text character by character
        var duration = currentFullText.Length / textSpeed;
        
        textTween?.Kill();
        textTween = CreateTween();
        textTween.TweenMethod(Callable.From<int>(UpdateDisplayedText), 0, currentFullText.Length, duration);
        textTween.TweenCallback(Callable.From(OnTextAnimationComplete));
    }
    
    private void UpdateDisplayedText(int charCount)
    {
        if (textDisplay == null) return;
        
        var displayText = currentFullText.Substring(0, charCount);
        textDisplay.Text = displayText;
        
        // Auto-scroll when text fills display
        if (textDisplay.GetContentHeight() > textDisplay.Size.Y)
        {
            textDisplay.ScrollToLine(textDisplay.GetLineCount() - 1);
        }
    }
    
    private void CompleteTextAnimation()
    {
        textTween?.Kill();
        OnTextAnimationComplete();
    }
    
    private void OnTextAnimationComplete()
    {
        isTextAnimating = false;
        if (textDisplay != null)
            textDisplay.Text = currentFullText;
    }
    
    private void StopTextAnimation()
    {
        textTween?.Kill();
        isTextAnimating = false;
    }
    
    #endregion
    
    #region Choice System
    
    private void CreateChoiceButtons(string[] choices)
    {
        ClearChoiceButtons();
        
        if (choicesContainer == null) return;
        
        for (int i = 0; i < choices.Length; i++)
        {
            var button = CreateChoiceButton(choices[i], i);
            choicesContainer.AddChild(button);
            choiceButtons.Add(button);
        }
        
        currentChoiceIndex = 0;
        UpdateChoiceSelection();
    }
    
    private Button CreateChoiceButton(string text, int index)
    {
        var button = new Button();
        button.Text = text;
        button.FocusMode = Control.FocusModeEnum.All;
        
        // Connect button signal
        button.Pressed += () => MakeChoice(index, text);
        
        return button;
    }
    
    private void ShowChoiceInterface()
    {
        if (choicesContainer == null || choiceButtons.Count == 0) return;
        
        isShowingChoices = true;
        choicesContainer.Visible = true;
        
        // Focus first choice
        if (choiceButtons.Count > 0)
            choiceButtons[0].GrabFocus();
    }
    
    private void HideChoices()
    {
        if (choicesContainer == null) return;
        
        isShowingChoices = false;
        choicesContainer.Visible = false;
        ClearChoiceButtons();
    }
    
    private void NavigateChoices(int direction)
    {
        if (choiceButtons.Count == 0) return;
        
        currentChoiceIndex += direction;
        
        // Wrap around
        if (currentChoiceIndex < 0)
            currentChoiceIndex = choiceButtons.Count - 1;
        else if (currentChoiceIndex >= choiceButtons.Count)
            currentChoiceIndex = 0;
        
        UpdateChoiceSelection();
    }
    
    private void UpdateChoiceSelection()
    {
        if (choiceButtons.Count == 0) return;
        
        // Clear all focus
        foreach (var button in choiceButtons)
            button.ReleaseFocus();
        
        // Focus current choice
        if (currentChoiceIndex >= 0 && currentChoiceIndex < choiceButtons.Count)
            choiceButtons[currentChoiceIndex].GrabFocus();
    }
    
    private void MakeChoice(int index, string choiceText)
    {
        HideChoices();
        EmitSignal(SignalName.ChoiceMade, index, choiceText);
        GD.Print($"[NovelControls] Choice made: {index} - {choiceText}");
    }
    
    private void ClearChoiceButtons()
    {
        foreach (var button in choiceButtons)
            button.QueueFree();
        choiceButtons.Clear();
    }
    
    #endregion
    
    #region History & Cursor
    
    private void AddToHistory(string text)
    {
        if (!enableHistory || historyDisplay == null) return;
        
        dialogueHistory += text + "\n\n";
        historyDisplay.Text = dialogueHistory;
        
        // Auto-scroll history to bottom
        historyDisplay.ScrollToLine(historyDisplay.GetLineCount() - 1);
    }
    
    private Vector2 GetCurrentCursorPosition()
    {
        if (isShowingChoices && choiceButtons.Count > 0 && currentChoiceIndex < choiceButtons.Count)
        {
            // Cursor at current choice button
            return choiceButtons[currentChoiceIndex].GlobalPosition + choiceButtons[currentChoiceIndex].Size * 0.5f;
        }
        else if (textDisplay != null)
        {
            // Cursor at text display - determine orientation based on text state
            var cursorPos = GetTextCursorPosition();
            return cursorPos;
        }
        
        return GlobalPosition + Size * 0.5f; // Fallback to center
    }
    
    private Vector2 GetTextCursorPosition()
    {
        if (textDisplay == null) return Vector2.Zero;
        
        // Position cursor at end of text
        var textRect = textDisplay.GetGlobalRect();
        
        if (IsAtEndOfParagraph())
        {
            // Cursor should be rotated 90 degrees downward (handled by InputManager)
            return new Vector2(textRect.Position.X + textRect.Size.X * 0.5f, textRect.Position.Y + textRect.Size.Y);
        }
        else
        {
            // Normal sideways cursor
            return new Vector2(textRect.Position.X + textRect.Size.X, textRect.Position.Y + textRect.Size.Y * 0.5f);
        }
    }
    
    private bool IsAtEndOfParagraph()
    {
        // Simple check: if text animation is complete and not showing choices
        return !isTextAnimating && !isShowingChoices;
    }
    
    #endregion
}