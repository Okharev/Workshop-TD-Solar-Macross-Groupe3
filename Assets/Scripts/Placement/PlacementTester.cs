using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Placement
{
    public class PlacementTester : MonoBehaviour
    {
        [Header("Dependencies")] [SerializeField]
        private PlacementManager placementManager;

        [Header("Test Data")] [Tooltip("Glisse tes Blueprints de tours ici pour les tester")] [SerializeField]
        private List<BuildingSo> testTowers;

        private void Update()
        {
            // Raccourcis Clavier pour tester rapidement
            // Touche '1' pour la première tour, '2' pour la deuxième, etc.
            if (testTowers.Count > 0 && Keyboard.current.digit1Key.wasPressedThisFrame) SelectTower(0);
            if (testTowers.Count > 1 && Keyboard.current.digit2Key.wasPressedThisFrame) SelectTower(1);
            if (testTowers.Count > 2 && Keyboard.current.digit3Key.wasPressedThisFrame) SelectTower(2);

            // Touche 'Echap' gérée par le Manager, mais on peut forcer l'arrêt ici aussi si besoin
            if (Keyboard.current.backspaceKey.wasPressedThisFrame)
            {
                placementManager.StopPlacement();
                Debug.Log("Placement stopped via Tester.");
            }
        }

        // Cette méthode peut aussi être appelée par des boutons dans l'Inspector (voir étape suivante)
        public void SelectTower(int index)
        {
            if (index < 0 || index >= testTowers.Count)
            {
                Debug.LogWarning($"Index {index} invalide. Ajoute des tours dans la liste 'Test Towers'.");
                return;
            }

            Debug.Log($"Testing Tower: {testTowers[index].name}");
            placementManager.StartPlacement(testTowers[index]);
        }
    }
}