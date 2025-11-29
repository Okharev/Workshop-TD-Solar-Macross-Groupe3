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
            if (testTowers.Count > 0 && Keyboard.current.digit1Key.wasPressedThisFrame) SelectTower(0);
            if (testTowers.Count > 1 && Keyboard.current.digit2Key.wasPressedThisFrame) SelectTower(1);
            if (testTowers.Count > 2 && Keyboard.current.digit3Key.wasPressedThisFrame) SelectTower(2);

            if (Keyboard.current.backspaceKey.wasPressedThisFrame)
            {
                placementManager.StopPlacement();
                Debug.Log("Placement stopped via Tester.");
            }
        }

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