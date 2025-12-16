using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class PauseMenuController : MonoBehaviour
{
    [Header("UI Document")]
    private UIDocument _uiDocument;

    [Header("Carousel Data")]
    [Tooltip("Add your control scheme images here.")]
    public List<Texture2D> controlImages; 
    private int _carouselIndex = 0;

    // --- Visual Elements References ---
    private VisualElement _overlay;
    
    // Containers
    private VisualElement _menuContainer;
    private VisualElement _optionsContainer;
    private VisualElement _controlsContainer;
    private DropdownField _drpWindowMode;
    private DropdownField _drpResolution;
    private Resolution[] _availableResolutions;
    // Carousel Elements
    private VisualElement _carouselImage;
    private Label _lblPage;

    // Options Elements
    private DropdownField _drpGraphics;
    private Button _btnBindUp, _btnBindDown, _btnBindLeft, _btnBindRight;

    // State
    private bool _isPaused = false;
    private Button _activeRebindButton = null; // To track if we are waiting for a key

    private void Awake()
    {
        _uiDocument = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        // 1. Get Root
        var root = _uiDocument.rootVisualElement;
        if (root == null)
        {
            Debug.LogError("No Root VisualElement found! Check your UIDocument.");
            return;
        }

        // 2. Find Main Containers
        _overlay = root.Q<VisualElement>("Overlay");
        _menuContainer = root.Q<VisualElement>("MenuContainer");
        _optionsContainer = root.Q<VisualElement>("OptionsContainer");
        _controlsContainer = root.Q<VisualElement>("ControlsContainer");

        // 3. Setup Main Menu Buttons
        root.Q<Button>("BtnResume").clicked += ResumeGame;
        root.Q<Button>("BtnOptions").clicked += OpenOptions;
        root.Q<Button>("BtnControls").clicked += OpenControls;
        root.Q<Button>("BtnLeave").clicked += LeaveGame;

        // 4. Setup Sub-Menu Back Buttons
        root.Q<Button>("BtnBack").clicked += BackToMainMenu;         // Inside Options
        root.Q<Button>("BtnControlsBack").clicked += BackToMainMenu; // Inside Controls

        // 5. Setup Carousel
        _carouselImage = root.Q<VisualElement>("CarouselImage");
        _lblPage = root.Q<Label>("LblPage");
        
        // Safety check for buttons (in case UXML isn't updated yet)
        var btnPrev = root.Q<Button>("BtnPrev");
        var btnNext = root.Q<Button>("BtnNext");
        if(btnPrev != null) btnPrev.clicked += OnCarouselPrev;
        if(btnNext != null) btnNext.clicked += OnCarouselNext;

        // 6. Setup Graphics Dropdown
        _drpGraphics = root.Q<DropdownField>("DrpGraphics");
        if (_drpGraphics != null)
        {
            _drpGraphics.choices = new List<string>(QualitySettings.names);
            _drpGraphics.index = QualitySettings.GetQualityLevel();
            _drpGraphics.RegisterValueChangedCallback(evt => 
            {
                QualitySettings.SetQualityLevel(_drpGraphics.choices.IndexOf(evt.newValue), true);
            });
        }

        // 7. Setup Key Rebinding Buttons
        _btnBindUp = root.Q<Button>("BtnBindUp");
        _btnBindDown = root.Q<Button>("BtnBindDown");
        _btnBindLeft = root.Q<Button>("BtnBindLeft");
        _btnBindRight = root.Q<Button>("BtnBindRight");

        if (_btnBindUp != null)
        {
            _btnBindUp.clicked += () => StartRebind("UP", _btnBindUp);
            _btnBindDown.clicked += () => StartRebind("DOWN", _btnBindDown);
            _btnBindLeft.clicked += () => StartRebind("LEFT", _btnBindLeft);
            _btnBindRight.clicked += () => StartRebind("RIGHT", _btnBindRight);
            
            // Initial Label Update
            UpdateKeyBindingLabels();
        }
        
        SetupVideoOptions(root);

        // Initialize State: Game Running, Menu Hidden
        ResumeGame();
    }

    private void SetupVideoOptions(VisualElement root)
{
    // --- 1. Window Mode Setup ---
    _drpWindowMode = root.Q<DropdownField>("DrpWindowMode");
    
    // Define the modes we want to offer
    var modes = new List<string> { "Fullscreen", "Borderless", "Windowed" };
    _drpWindowMode.choices = modes;

    // Set initial value based on current state
    if (Screen.fullScreenMode == FullScreenMode.ExclusiveFullScreen) _drpWindowMode.index = 0;
    else if (Screen.fullScreenMode == FullScreenMode.FullScreenWindow) _drpWindowMode.index = 1;
    else _drpWindowMode.index = 2;

    // Handle Change
    _drpWindowMode.RegisterValueChangedCallback(evt =>
    {
        switch (evt.newValue)
        {
            case "Fullscreen":
                Screen.fullScreenMode = FullScreenMode.ExclusiveFullScreen;
                break;
            case "Borderless":
                Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
                break;
            case "Windowed":
                Screen.fullScreenMode = FullScreenMode.Windowed;
                break;
        }
    });

    // --- 2. Resolution Setup ---
    _drpResolution = root.Q<DropdownField>("DrpResolution");
    
    // Get all resolutions supported by the monitor
    _availableResolutions = Screen.resolutions;
    
    // Create a list of strings "Width x Height" (e.g. "1920 x 1080")
    // We reverse the list so high resolutions appear at the top
    List<string> options = new List<string>();
    int currentResolutionIndex = 0;
    
    // Simple filter to remove duplicates (same resolution, different refresh rates)
    // We only keep the entries where refresh rate is 0 (generic) or max available.
    // For simplicity, we just look at width/height distinct values here:
    
    List<Resolution> uniqueResolutions = new List<Resolution>();
    
    for (int i = 0; i < _availableResolutions.Length; i++)
    {
        Resolution res = _availableResolutions[i];
        
        // Check if we already added this width/height combo
        bool alreadyAdded = false;
        foreach(var u in uniqueResolutions)
        {
            if (u.width == res.width && u.height == res.height)
            {
                alreadyAdded = true;
                break;
            }
        }
        
        if (!alreadyAdded)
        {
            uniqueResolutions.Add(res);
        }
    }

    // Populate Dropdown
    for (int i = 0; i < uniqueResolutions.Count; i++)
    {
        string option = uniqueResolutions[i].width + " x " + uniqueResolutions[i].height;
        options.Add(option);

        if (uniqueResolutions[i].width == Screen.width && 
            uniqueResolutions[i].height == Screen.height)
        {
            currentResolutionIndex = i;
        }
    }

    _drpResolution.choices = options;
    _drpResolution.index = currentResolutionIndex;

    // Handle Change
    _drpResolution.RegisterValueChangedCallback(evt =>
    {
        // Find the resolution object that matches the selected string index
        // Note: Using 'index' is safer than parsing the string
        int selectedIndex = _drpResolution.index;
        
        // Safety check
        if (selectedIndex >= 0 && selectedIndex < uniqueResolutions.Count)
        {
            Resolution res = uniqueResolutions[selectedIndex];
            
            // Apply Resolution
            // We pass the current fullscreen mode so it doesn't reset to windowed
            Screen.SetResolution(res.width, res.height, Screen.fullScreenMode);
        }
    });
}
    
    private void Update()
    {
        // If we are currently waiting for a key rebind, do NOT toggle pause
        if (_activeRebindButton != null) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // Logic: 
            // 1. If inside Options/Controls -> Go Back to Main Menu
            // 2. If inside Main Menu -> Resume Game
            // 3. If Game -> Pause & Open Menu

            if (_isPaused && (IsOptionsVisible() || IsControlsVisible()))
            {
                BackToMainMenu();
            }
            else if (_isPaused)
            {
                ResumeGame();
            }
            else
            {
                PauseGame();
            }
        }
    }

    // --- MAIN FLOW ---

    private void PauseGame()
    {
        _isPaused = true;
        Time.timeScale = 0f; // Freeze game
        
        _overlay.RemoveFromClassList("hidden");
        ShowContainer(_menuContainer);
    }

    private void ResumeGame()
    {
        _isPaused = false;
        Time.timeScale = 1f; // Unfreeze game
        
        _overlay.AddToClassList("hidden");
        
        // Hide sub-menus just in case
        _optionsContainer?.AddToClassList("hidden");
        _controlsContainer?.AddToClassList("hidden");
    }

    private void LeaveGame()
    {
        Debug.Log("Quitting...");
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }

    // --- NAVIGATION HELPERS ---

    private void OpenOptions() => ShowContainer(_optionsContainer);

    private void OpenControls()
    {
        ShowContainer(_controlsContainer);
        _carouselIndex = 0; // Reset to first page
        UpdateCarouselUI();
    }

    private void BackToMainMenu() => ShowContainer(_menuContainer);

    private void ShowContainer(VisualElement containerToShow)
    {
        // Hide all containers
        _menuContainer.AddToClassList("hidden");
        _optionsContainer.AddToClassList("hidden");
        _controlsContainer.AddToClassList("hidden");

        // Show specific one
        containerToShow.RemoveFromClassList("hidden");
    }

    private bool IsOptionsVisible() => !_optionsContainer.ClassListContains("hidden");
    private bool IsControlsVisible() => !_controlsContainer.ClassListContains("hidden");

    // --- CAROUSEL LOGIC ---

    private void OnCarouselNext()
    {
        if (controlImages == null || controlImages.Count == 0) return;
        _carouselIndex = (_carouselIndex + 1) % controlImages.Count;
        UpdateCarouselUI();
    }

    private void OnCarouselPrev()
    {
        if (controlImages == null || controlImages.Count == 0) return;
        _carouselIndex--;
        if (_carouselIndex < 0) _carouselIndex = controlImages.Count - 1;
        UpdateCarouselUI();
    }

    private void UpdateCarouselUI()
    {
        if (controlImages == null || controlImages.Count == 0) return;
        
        if (_carouselImage != null)
            _carouselImage.style.backgroundImage = new StyleBackground(controlImages[_carouselIndex]);
            
        if (_lblPage != null)
            _lblPage.text = $"{_carouselIndex + 1} / {controlImages.Count}";
    }

    // --- REBINDING LOGIC ---

    private void UpdateKeyBindingLabels()
    {
        if (_btnBindUp == null) return;
        // _btnBindUp.text = KeyBindings.MoveUp.ToString();
        // _btnBindDown.text = KeyBindings.MoveDown.ToString();
        // _btnBindLeft.text = KeyBindings.MoveLeft.ToString();
        // _btnBindRight.text = KeyBindings.MoveRight.ToString();
    }

    private void StartRebind(string actionName, Button btnClicked)
    {
        if (_activeRebindButton != null) return; // Prevent double click

        _activeRebindButton = btnClicked;
        btnClicked.text = "..."; // Visual Feedback
        StartCoroutine(WaitForKeyPress(actionName));
    }

    private IEnumerator WaitForKeyPress(string actionName)
    {
        // Wait for one frame to avoid registering the mouse click as a key
        yield return null; 

        bool keyFound = false;

        // Loop until a key is pressed
        while (!keyFound)
        {
            if (Input.anyKeyDown)
            {
                foreach (KeyCode kcode in System.Enum.GetValues(typeof(KeyCode)))
                {
                    if (Input.GetKeyDown(kcode))
                    {
                        // Assign Key
                        switch (actionName)
                        {
                            //  case "UP": KeyBindings.MoveUp = kcode; break;
                            //  case "DOWN": KeyBindings.MoveDown = kcode; break;
                            //  case "LEFT": KeyBindings.MoveLeft = kcode; break;
                            //  case "RIGHT": KeyBindings.MoveRight = kcode; break;
                        }
                        keyFound = true;
                        break;
                    }
                }
            }
            yield return null;
        }

        UpdateKeyBindingLabels();
        _activeRebindButton = null;
    }
}