using Enemy;
using UnityEngine;
using UnityEngine.UIElements;

// Important pour accéder à WaveManager

namespace UI
{
    [RequireComponent(typeof(UIDocument))]
    public class WavePanelController : MonoBehaviour
    {
        [Header("Dependencies")] [Tooltip("Référence au WaveManager de la scène.")]
        public WaveManager waveManager;

        private Button _nextWaveButton;

        // Références aux éléments visuels (Visual Elements)
        private VisualElement _root;
        private Label _statusLabel;
        private Label _waveIndexLabel;
        private Label _waveNameLabel;

        private void OnEnable()
        {
            // 1. Récupération de la racine UI
            var uiDocument = GetComponent<UIDocument>();
            if (uiDocument == null) return;

            _root = uiDocument.rootVisualElement;

            // 2. Assignation des éléments via leur nom (défini dans le UXML)
            _statusLabel = _root.Q<Label>("wave-status-label");
            _waveIndexLabel = _root.Q<Label>("wave-index-label");
            _waveNameLabel = _root.Q<Label>("wave-name-label");
            _nextWaveButton = _root.Q<Button>("next-wave-btn");

            // 3. Abonnement au bouton UI
            if (_nextWaveButton != null) _nextWaveButton.clicked += OnNextWaveClicked;

            // 4. Abonnement aux événements du jeu (WaveManager)
            if (waveManager != null)
            {
                waveManager.OnWaveStarted += HandleWaveStarted;
                waveManager.OnWaveFinished += HandleWaveFinished;
                waveManager.OnAllWavesCompleted += HandleAllWavesCompleted;
            }

            // Initialisation de l'état visuel
            UpdateUIState(false);
        }

        private void OnDisable()
        {
            // Toujours se désabonner pour éviter les fuites de mémoire
            if (_nextWaveButton != null) _nextWaveButton.clicked -= OnNextWaveClicked;

            if (waveManager != null)
            {
                waveManager.OnWaveStarted -= HandleWaveStarted;
                waveManager.OnWaveFinished -= HandleWaveFinished;
                waveManager.OnAllWavesCompleted -= HandleAllWavesCompleted;
            }
        }

        // --- Event Handlers ---

        private void OnNextWaveClicked()
        {
            if (waveManager != null && !waveManager.IsWaveActive) waveManager.StartNextWave();
        }

        private void HandleWaveStarted(int index, string waveName)
        {
            _waveIndexLabel.text = $"WAVE {index}";
            _waveNameLabel.text = waveName;
            _statusLabel.text = "WAVE IN PROGRESS";
            _statusLabel.style.color = new StyleColor(Color.red); // Change la couleur en rouge

            // On cache le bouton pendant la vague
            if (_nextWaveButton != null) _nextWaveButton.AddToClassList("hidden");
        }

        private void HandleWaveFinished()
        {
            _statusLabel.text = "WAVE COMPLETE";
            _statusLabel.style.color = new StyleColor(Color.green); // Change la couleur en vert

            // On réaffiche le bouton
            if (_nextWaveButton != null) _nextWaveButton.RemoveFromClassList("hidden");
        }

        private void HandleAllWavesCompleted()
        {
            _statusLabel.text = "VICTORY";
            _waveNameLabel.text = "All waves defeated!";
            _statusLabel.style.color = new StyleColor(Color.yellow);

            // On cache le bouton définitivement
            if (_nextWaveButton != null) _nextWaveButton.AddToClassList("hidden");
        }

        // --- Helpers ---

        private void UpdateUIState(bool isWaveActive)
        {
            // Initialisation au lancement du jeu
            if (waveManager == null) return;

            var displayIndex = waveManager.CurrentWaveIndex + 1;
            // Si index est -1 (pas commencé), on affiche 0 ou 1 selon préférence
            if (displayIndex == 0) displayIndex = 1;

            _waveIndexLabel.text = $"WAVE {displayIndex}";
            _waveNameLabel.text = "Ready to start...";

            // Si une vague est active (rare au start, mais possible), on cache le bouton
            if (isWaveActive) _nextWaveButton.AddToClassList("hidden");
            else _nextWaveButton.RemoveFromClassList("hidden");
        }
    }
}