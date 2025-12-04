using Enemy;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI
{
    [RequireComponent(typeof(UIDocument))]
    public class WavePanelController : MonoBehaviour
    {
        [Header("Dependencies")] [Tooltip("Référence au WaveManager de la scène.")]
        public WaveManager waveManager;

        private Button _nextWaveButton;

        private VisualElement _root;
        private Label _statusLabel;
        private Label _waveIndexLabel;
        private Label _waveNameLabel;

        private void OnEnable()
        {
            var uiDocument = GetComponent<UIDocument>();
            if (uiDocument == null) return;

            _root = uiDocument.rootVisualElement;

            _statusLabel = _root.Q<Label>("wave-status-label");
            _waveIndexLabel = _root.Q<Label>("wave-index-label");
            _waveNameLabel = _root.Q<Label>("wave-name-label");
            _nextWaveButton = _root.Q<Button>("next-wave-btn");

            if (_nextWaveButton != null) _nextWaveButton.clicked += OnNextWaveClicked;

            if (waveManager != null)
            {
                waveManager.OnWaveStarted += HandleWaveStarted;
                waveManager.OnWaveFinished += HandleWaveFinished;
                waveManager.OnAllWavesCompleted += HandleAllWavesCompleted;
            }

            UpdateUIState(false);
        }

        private void OnDisable()
        {
            if (_nextWaveButton != null) _nextWaveButton.clicked -= OnNextWaveClicked;

            if (waveManager != null)
            {
                waveManager.OnWaveStarted -= HandleWaveStarted;
                waveManager.OnWaveFinished -= HandleWaveFinished;
                waveManager.OnAllWavesCompleted -= HandleAllWavesCompleted;
            }
        }


        private void OnNextWaveClicked()
        {
            if (waveManager != null && !waveManager.IsWaveActive) waveManager.StartNextWave();
        }

        private void HandleWaveStarted(int index, string waveName)
        {
            _waveIndexLabel.text = $"WAVE {index}";
            _waveNameLabel.text = waveName;
            _statusLabel.text = "WAVE IN PROGRESS";
            _statusLabel.style.color = new StyleColor(Color.red); 
            if (_nextWaveButton != null) _nextWaveButton.AddToClassList("hidden");
        }

        private void HandleWaveFinished()
        {
            _statusLabel.text = "WAVE COMPLETE";
            _statusLabel.style.color = new StyleColor(Color.green);

            if (_nextWaveButton != null) _nextWaveButton.RemoveFromClassList("hidden");
        }

        private void HandleAllWavesCompleted()
        {
            _statusLabel.text = "VICTORY";
            _waveNameLabel.text = "All waves defeated!";
            _statusLabel.style.color = new StyleColor(Color.yellow);

            if (_nextWaveButton != null) _nextWaveButton.AddToClassList("hidden");
        }


        private void UpdateUIState(bool isWaveActive)
        {
            if (waveManager == null) return;

            var displayIndex = waveManager.CurrentWaveIndex + 1;
            if (displayIndex == 0) displayIndex = 1;

            _waveIndexLabel.text = $"WAVE {displayIndex}";
            _waveNameLabel.text = "Ready to start...";

            if (isWaveActive) _nextWaveButton.AddToClassList("hidden");
            else _nextWaveButton.RemoveFromClassList("hidden");
        }
    }
}