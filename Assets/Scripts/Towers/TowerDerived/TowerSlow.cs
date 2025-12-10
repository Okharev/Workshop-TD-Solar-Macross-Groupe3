using System.Collections;
using System.Collections.Generic;
using Economy;
using UnityEngine;

namespace Towers.TowerDerived
{
    public sealed class TowerSlow : BaseTower
    {
        [Header("Slow Configuration")] 
        [Tooltip("Percentage to slow the enemy. 0.3 = 30% slow.")] 
        [Range(0f, 0.9f)]
        public float slowPercent = 0.3f;

        [Header("Performance")] 
        [SerializeField]
        private float checkInterval = 0.2f;

        [SerializeField] private LayerMask enemyLayer;

        [Header("Visuals")]
        [Tooltip("Glisse ici l'objet enfant qui contient le mesh du rotor Daerrieus.")]
        [SerializeField] private Transform rotorModel;

        [Tooltip("Vitesse de rotation quand la tour est au repos (vent normal).")]
        public float idleSpeed = 20f;

        [Tooltip("Vitesse de rotation quand la tour ralentit des ennemis.")]
        public float activeSpeed = 300f;

        [Tooltip("A quel point la tour accélère/décélère (plus haut = plus réactif).")]
        public float acceleration = 2f;
        // ----------------------------------------

        private readonly List<EnemyController> _currentFrameEnemies = new();
        private readonly List<EnemyController> _enemiesToRemove = new();
        private readonly Collider[] _hitBuffer = new Collider[32];
        
        private readonly HashSet<EnemyController> _slowedEnemies = new();

        // Variable interne pour suivre la vitesse actuelle
        private float _currentRotationSpeed;

        protected override void Start()
        {
            if (enemyLayer == 0) enemyLayer = LayerMask.GetMask("EnemyAir", "EnemyGround");
            
            // Initialisation de la vitesse
            _currentRotationSpeed = idleSpeed;
            
            StartCoroutine(SlowLoop());
        }

        // Nous utilisons Update pour l'animation car elle doit être fluide (chaque frame)
        private void Update()
        {
            HandleRotation();
        }

        private void HandleRotation()
        {
            // Si pas de rotor assigné, on ne fait rien pour éviter les erreurs
            if (rotorModel == null) return;

            // 1. Déterminer la vitesse cible
            // Si on a des ennemis ralentis ET que la tour est alimentée -> Vitesse Rapide
            // Sinon -> Vitesse de Repos
            bool isActive = _slowedEnemies.Count > 0 && (powerSource == null || powerSource.IsPowered);
            float targetSpeed = isActive ? activeSpeed : idleSpeed;

            // 2. Transition fluide (Lerp) vers la vitesse cible
            _currentRotationSpeed = Mathf.Lerp(_currentRotationSpeed, targetSpeed, Time.deltaTime * acceleration);

            // 3. Appliquer la rotation
            rotorModel.Rotate(Vector3.up, _currentRotationSpeed * Time.deltaTime);
        }

        private void OnDestroy()
        {
            RemoveAllSlows();
            StopAllCoroutines();
        }

        protected override void OnDrawGizmosTower()
        {
            var r = range.Value;
            Gizmos.color = new Color(0, 0, 1, 0.3f);
            Gizmos.DrawSphere(transform.position, range.Value);

            Gizmos.color = Color.green;
            foreach (var nemy in _slowedEnemies) Gizmos.DrawLine(transform.position, nemy.transform.position);
        }

        private IEnumerator SlowLoop()
        {
            var wait = new WaitForSeconds(checkInterval);

            while (true)
            {
                if (!this) yield break;

                // Logic: Only apply slow if powered on
                if (powerSource && powerSource.IsPowered)
                    CheckForEnemies();
                else if (_slowedEnemies.Count > 0)
                    // If power is lost, immediately free everyone
                    RemoveAllSlows();

                yield return wait;
            }
        }

        private void CheckForEnemies()
        {
            // 1. Physics Check (NonAlloc for performance)
            var hitCount = Physics.OverlapSphereNonAlloc(transform.position, range.Value, _hitBuffer, enemyLayer);


            _currentFrameEnemies.Clear();

            // 2. Identify Valid Enemies in Range
            for (var i = 0; i < hitCount; i++)
            {
                var enemy = _hitBuffer[i].GetComponentInParent<EnemyController>();

                if (enemy)
                {
                    // Optional: Distance check if collider is larger than range
                    var dist = Vector3.Distance(transform.position, enemy.transform.position);
                    if (dist <= range.Value)
                    {
                        _currentFrameEnemies.Add(enemy);


                        // If this enemy is new to the set, apply the slow
                        if (!_slowedEnemies.Contains(enemy))
                        {
                            ApplySlow(enemy);
                            _slowedEnemies.Add(enemy);
                        }
                    }
                }
            }

            // 3. Cleanup: Find enemies in the "Slowed" list that are NOT in the "Current" list
            _enemiesToRemove.Clear();

            foreach (var slowedEnemy in _slowedEnemies)
                // If enemy died (null) OR is no longer in range list
                if (!slowedEnemy || !_currentFrameEnemies.Contains(slowedEnemy))
                    _enemiesToRemove.Add(slowedEnemy);

            // Remove modifiers
            foreach (var oldEnemy in _enemiesToRemove)
            {
                if (oldEnemy) RemoveSlow(oldEnemy);
                _slowedEnemies.Remove(oldEnemy);
            }
        }

        private void ApplySlow(EnemyController target)
        {
            var mod = new StatModifier(-slowPercent, StatModType.PercentAdd, this);

            target.speed.AddModifier(mod);
        }

        private void RemoveSlow(EnemyController target)
        {
            target.speed.RemoveAllModifiersFromSource(this);
        }

        private void RemoveAllSlows()
        {
            foreach (var enemy in _slowedEnemies)
                if (enemy)
                    RemoveSlow(enemy);
            _slowedEnemies.Clear();
        }

        protected override void Fire()
        {
        }

        protected override void AcquireTarget()
        {
        }
    }
}