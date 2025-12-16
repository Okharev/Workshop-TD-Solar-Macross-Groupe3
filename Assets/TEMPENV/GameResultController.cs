using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using Enemy;
using Placement;

namespace UI
{
    [RequireComponent(typeof(UIDocument))]
    public class GameResultController : MonoBehaviour
    {
        [Header("Scene Configuration")]
        public string mainMenuSceneName = "MainMenu";

        [Header("Game References")]
        [Tooltip("Glisse ici l'objet (Base) que le joueur doit protéger.")]
        [SerializeField] private DestructibleObjective _targetBase;

        [Header("Art Assets")]
        public Texture2D winImage;
        public Texture2D loseImage;

        [Header("Debug")]
        [Tooltip("Coche pour afficher les boutons de test en haut à gauche de l'écran")]
        public bool showDebugButtons = true;

        private UIDocument _uiDocument;
        private VisualElement _rootContainer;
        private VisualElement _resultImageElement;
        
        private Button _restartBtn;
        private Button _menuBtn;
        private Button _quitBtn;

        private void OnEnable()
        {
            _uiDocument = GetComponent<UIDocument>();
            if (_uiDocument == null) return;

            var root = _uiDocument.rootVisualElement;
            _rootContainer = root.Q<VisualElement>("RootContainer");
            _resultImageElement = root.Q<VisualElement>("ResultImage");

            _restartBtn = root.Q<Button>("RestartButton");
            _menuBtn = root.Q<Button>("MenuButton");
            _quitBtn = root.Q<Button>("QuitButton");

            if (_restartBtn != null) _restartBtn.clicked += OnRestartClicked;
            if (_menuBtn != null) _menuBtn.clicked += OnMenuClicked;
            if (_quitBtn != null) _quitBtn.clicked += OnQuitClicked;
            
            if (_rootContainer != null) _rootContainer.style.display = DisplayStyle.None;

            // Abonnements
            if (WaveManager.Instance != null)
                WaveManager.Instance.OnAllWavesCompleted += ShowWinScreen;

            if (_targetBase != null)
                _targetBase.OnDestroyed += ShowGameOverScreen;
        }

        private void OnDisable()
        {
            if (WaveManager.Instance != null)
                WaveManager.Instance.OnAllWavesCompleted -= ShowWinScreen;

            if (_targetBase != null)
                _targetBase.OnDestroyed -= ShowGameOverScreen;
            
            if (_restartBtn != null) _restartBtn.clicked -= OnRestartClicked;
            if (_menuBtn != null) _menuBtn.clicked -= OnMenuClicked;
            if (_quitBtn != null) _quitBtn.clicked -= OnQuitClicked;
        }

        // --- Méthodes d'affichage ---

        private void ShowGameOverScreen()
        {
            if (_rootContainer == null) return;
            // Évite de ré-afficher si déjà ouvert
            if (_rootContainer.style.display == DisplayStyle.Flex) return;

            Time.timeScale = 0f;
            SetupUI(loseImage);
            ShowRoot();
        }

        private void ShowWinScreen()
        {
            if (_rootContainer == null) return;
            if (_rootContainer.style.display == DisplayStyle.Flex) return;

            Time.timeScale = 0f;
            SetupUI(winImage);
            ShowRoot();
        }

        private void SetupUI(Texture2D bgImage)
        {
            
            if (_rootContainer != null)
            {
                if (bgImage != null)
                {
                    Debug.Log($"Chargement de l'image : {bgImage.name}");
                    // 1. On assigne l'image
                    _rootContainer.style.backgroundImage = new StyleBackground(bgImage);
                    // 2. IMPORTANT : On met la couleur à BLANC pour ne pas teinter l'image
                    _rootContainer.style.backgroundColor = Color.white; 
                }
                else
                {
                    Debug.LogWarning("Aucune image assignée dans l'inspecteur ! Utilisation d'un fond noir.");
                    // Pas d'image ? On met un fond noir pour éviter l'écran blanc aveuglant
                    _rootContainer.style.backgroundImage = null;
                    _rootContainer.style.backgroundColor = Color.black;
                }
            }
        }

        private void ShowRoot()
        {
            // Étape 1 : On rend l'élément visible (layout)
            _rootContainer.style.display = DisplayStyle.Flex;
            
            // Étape 2 : On s'assure qu'il est transparent avant de commencer
            _rootContainer.style.opacity = 0f;

            // Étape 3 : On lance l'animation
            StartCoroutine(FadeInRoutine());
        }

        private IEnumerator FadeInRoutine()
        {
            // On attend une frame pour que Unity calcule le Layout et charge l'image
            yield return null; 
            
            // On passe à l'opacité 1 (la transition CSS de 0.5s fera le reste)
            _rootContainer.style.opacity = 1f;
        }

        // --- Callbacks Boutons UI ---

        private void OnRestartClicked()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void OnMenuClicked()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(mainMenuSceneName);
        }

        private void OnQuitClicked()
        {
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        // ========================================================================
        // --- ZONE DE DEBUG ---
        // ========================================================================

        // Option 1 : Boutons visuels à l'écran (Legacy IMGUI)
        private void OnGUI()
        {
            if (!showDebugButtons) return;

            // Crée une zone en haut à gauche
            GUILayout.BeginArea(new Rect(10, 10, 150, 100));

            // Ajoute un fond sombre pour lisibilité
            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("Force LOSE"))
            {
                ShowGameOverScreen();
            }

            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("Force WIN"))
            {
                ShowWinScreen();
            }

            GUILayout.EndArea();
        }

        // Option 2 : Menu contextuel (Clic droit sur le script dans l'inspecteur)
        [ContextMenu("DEBUG: Force Win Screen")]
        public void DebugWin() => ShowWinScreen();

        [ContextMenu("DEBUG: Force Lose Screen")]
        public void DebugLose() => ShowGameOverScreen();
    }
}