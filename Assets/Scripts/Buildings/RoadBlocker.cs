using UnityEngine;

namespace Buildings
{
    public sealed class RoadBlocker : MonoBehaviour
    {
        [SerializeField] private bool isBlocked;
        
        [Header("References")]
        [Tooltip("L'objet physique qui bloque le chemin (ex: une barrière avec NavMeshObstacle)")]
        [SerializeField] private Transform toLockUnlock;
        
        [Tooltip("Lumière ou indicateur visuel (Optionnel)")]
        [SerializeField] private Light indicator;

        public bool IsBlocked
        {
            get => isBlocked;
            set
            {
                isBlocked = value;
                UpdateBlockerState();
            }
        }

        private void Awake()
        {
            // Assure que l'état visuel correspond à la variable au démarrage
            UpdateBlockerState();
        }

        private void OnValidate()
        {
            // Met à jour en temps réel dans l'éditeur
            UpdateBlockerState();
        }

        private void UpdateBlockerState()
        {
            if (indicator) indicator.gameObject.SetActive(!isBlocked);
            if (toLockUnlock) toLockUnlock.gameObject.SetActive(isBlocked);
        }
    }
}