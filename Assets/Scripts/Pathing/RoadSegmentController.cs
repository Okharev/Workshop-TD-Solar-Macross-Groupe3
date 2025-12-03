using UnityEngine;

namespace Pathing
{
    public class RoadSegmentController : MonoBehaviour
    {
        [SerializeField] private int splineIndex;
        [SerializeField] private GameObject blockerInstance;
        [SerializeField] private bool isBlocked;
        [SerializeField] private RoadNetworkGenerator generator; // Reference to parent

        public int SplineIndex => splineIndex;

        // Property wrapper to allow changing via code or inspector
        public bool IsBlocked
        {
            get => isBlocked;
            set
            {
                // Prevent infinite recursion if the generator calls this setter
                if (isBlocked == value) return;

                isBlocked = value;
                UpdateBlockerState();

                // --- Notify the generator so it knows the state changed ---
                if (generator) generator.SetRoadBlocked(splineIndex, isBlocked);
            }
        }

        // Allow toggling in the inspector at runtime
        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                UpdateBlockerState();
                // Ensure generator is kept in sync if we click the checkbox in Inspector
                if (generator) generator.SetRoadBlocked(splineIndex, isBlocked);
            }
        }

        // Use this method when the Network Generator forces a state change
        // to avoid calling back to the generator.
        public void SetBlockedInternal(bool blocked)
        {
            isBlocked = blocked;
            UpdateBlockerState();
        }

        public void Initialize(RoadNetworkGenerator owner, int index, GameObject blocker, bool initialBlocked)
        {
            generator = owner;
            splineIndex = index;
            blockerInstance = blocker;
            isBlocked = initialBlocked;
            UpdateBlockerState();
        }

        private void UpdateBlockerState()
        {
            if (blockerInstance)
                if (blockerInstance.activeSelf != isBlocked)
                    blockerInstance.SetActive(isBlocked);
        }
    }
}