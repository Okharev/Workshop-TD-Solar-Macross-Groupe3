using UnityEngine;

namespace Enemy
{
    [RequireComponent(typeof(HealthComponent))]
    [RequireComponent(typeof(EnemyObjectiveTracker))]
    public class EnemyTransformer : MonoBehaviour
    {
        [Header("Transformation Settings")] [Tooltip("Le Prefab COMPLET du Jet à faire apparaître")]
        public GameObject jetPrefab;

        [Range(0f, 1f)] public float swapThreshold = 0.5f; // 50% PV

        [Tooltip("Effet de particules lors de la transformation")]
        public GameObject transformVfx;

        private bool _hasSwapped;

        private HealthComponent _health;
        private EnemyObjectiveTracker _tracker;

        private void Awake()
        {
            _health = GetComponent<HealthComponent>();
            _tracker = GetComponent<EnemyObjectiveTracker>();
        }

        private void Start()
        {
            // On surveille les PV
            _health.CurrentHealth.Subscribe(CheckPhase).AddTo(this);
        }

        private void CheckPhase(int currentHp)
        {
            if (_hasSwapped) return;

            var ratio = (float)currentHp / _health.MaxHealth;

            if (ratio <= swapThreshold) PerformSwap();
        }

        private void PerformSwap()
        {
            _hasSwapped = true;
            Debug.Log($"[PhaseSwapper] {name} passe en Phase 2 (Jet) !");

            // 1. Instancier le Jet à la même position et rotation
            // On ajoute un petit décalage vers le haut (+5) pour qu'il ne spawn pas DANS le sol
            var spawnPos = transform.position + Vector3.up * 5f;
            var spawnRot = transform.rotation;

            var newJet = Instantiate(jetPrefab, spawnPos, spawnRot);

            // 2. Jouer un effet visuel (Explosion de fumée ?)
            if (transformVfx) Instantiate(transformVfx, transform.position, Quaternion.identity);

            // 3. TRANSFERT DE DONNÉES (Crucial)
            // On veut que le Jet sache immédiatement quoi attaquer sans scanner
            var oldTarget = _tracker.CurrentTarget.Value;

            // On récupère le tracker du NOUVEAU jet
            var newTracker = newJet.GetComponent<EnemyObjectiveTracker>();

            if (newTracker && oldTarget != null)
                newTracker.Initialize(_tracker.CurrentObjective, _tracker.BackupObjective);

            // 4. Détruire l'unité au sol
            Destroy(gameObject);
        }
    }
}