using UnityEngine;
using Buildings; // Nécessaire pour RoadBlocker

namespace Pathing
{
    public sealed class RoadSegmentController : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private int splineIndex;
        
        [Header("State")]
        [SerializeField] private bool isBlocked;
        [SerializeField] private RoadBlocker connectedBlocker; // Référence directe au script
        [SerializeField] private RoadNetworkGenerator generator;

        public int SplineIndex => splineIndex;

        public bool IsBlocked
        {
            get => isBlocked;
            set
            {
                // Protection contre la récursion infinie
                if (isBlocked == value) return;

                isBlocked = value;
                
                // 1. Appliquer visuellement sur le bloqueur
                UpdateBlockerVisuals();

                // 2. Notifier le générateur (si c'est lui qui gère la logique globale)
                if (generator) generator.SetRoadBlocked(splineIndex, isBlocked);
            }
        }

        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                // Permet de tester en cochant la case dans l'inspecteur Unity
                UpdateBlockerVisuals();
                if (generator) generator.SetRoadBlocked(splineIndex, isBlocked);
            }
        }

        /// <summary>
        /// Appelé par le générateur pour configurer ce segment après la création.
        /// </summary>
        public void Initialize(RoadNetworkGenerator owner, int index, RoadBlocker blockerScript, bool initialBlocked)
        {
            generator = owner;
            splineIndex = index;
            connectedBlocker = blockerScript;
            
            // On force l'état initial sans notifier le générateur pour éviter une boucle au démarrage
            isBlocked = initialBlocked;
            UpdateBlockerVisuals();
        }

        /// <summary>
        /// Utilisé quand le Générateur force un changement d'état (pour ne pas le re-notifier).
        /// </summary>
        public void SetBlockedInternal(bool blocked)
        {
            isBlocked = blocked;
            UpdateBlockerVisuals();
        }

        private void UpdateBlockerVisuals()
        {
            if (connectedBlocker != null)
            {
                connectedBlocker.IsBlocked = isBlocked;
            }
        }
    }
}