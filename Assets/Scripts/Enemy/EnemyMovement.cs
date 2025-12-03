using UnityEngine;
using UnityEngine.AI;
using Pathing.Gameplay;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(EnemyObjectiveTracker))]
public class EnemyMovement : MonoBehaviour
{
    private NavMeshAgent _agent;
    private EnemyObjectiveTracker _tracker;
    private Transform _currentTargetTransform;

    // Optimisation : Pour ne pas recalculer le chemin inutilement
    private Vector3 _lastKnownTargetPosition;
    private float _repathThreshold = 1.0f; // Distance min pour recalculer

    private void Start()
    {
        _agent = GetComponent<NavMeshAgent>();
        _tracker = GetComponent<EnemyObjectiveTracker>();

        // Configuration pour éviter le "glissement"
        _agent.autoBraking = false; // Ne pas ralentir à l'approche (pour foncer dedans)
        _agent.stoppingDistance = 2.0f; // S'arrêter un peu avant pour attaquer

        // Abonnement Réactif
        _tracker.CurrentTarget.Subscribe(newTarget => 
        {
            _currentTargetTransform = newTarget;
            if (newTarget != null)
            {
                UpdatePathImmediate(newTarget.position);
            }
            else
            {
                _agent.ResetPath();
            }
        }).AddTo(this);
    }
    
    private void Update()
    {
        // Si on a une cible active
        if (_currentTargetTransform != null)
        {
            // On vérifie la distance entre la dernière position connue et la position actuelle de la cible
            // sqrMagnitude est plus rapide que Distance (pas de racine carrée)
            if (Vector3.SqrMagnitude(_currentTargetTransform.position - _lastKnownTargetPosition) > _repathThreshold * _repathThreshold)
            {
                UpdatePathImmediate(_currentTargetTransform.position);
            }
        }
    }

    private void UpdatePathImmediate(Vector3 targetPos)
    {
        _lastKnownTargetPosition = targetPos;
        _agent.SetDestination(targetPos);
    }
}