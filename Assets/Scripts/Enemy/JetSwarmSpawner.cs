using System.Collections.Generic;
using Enemy;
using UnityEngine;

public class JetSwarmSpawner : MonoBehaviour
{
    [Header("1. Spawn Settings")] public GameObject jetPrefab;

    [Range(1, 20)] public int jetCount = 5;

    [Header("2. Patrol Zone")] public int waypointCount = 8;

    public Vector3 spawnAreaSize = new(200f, 50f, 200f);
    public float heightOffset = 50f;

    [Tooltip("Minimum distance between waypoints to ensure smooth turning")]
    public float minWaypointDistance = 40f;

    [Header("3. Debug")] public bool showGizmos = true;

    private readonly List<Transform> _generatedWaypoints = new();

    private void Start()
    {
        GenerateWaypoints();
        SpawnJets();
    }

    private void OnDrawGizmos()
    {
        if (!showGizmos) return;

        Gizmos.color = new Color(0, 1, 1, 0.3f);
        Gizmos.DrawWireCube(transform.position + Vector3.up * heightOffset, spawnAreaSize);

        if (_generatedWaypoints != null)
        {
            Gizmos.color = Color.yellow;
            for (var i = 0; i < _generatedWaypoints.Count; i++)
            {
                var wp = _generatedWaypoints[i];
                if (wp != null) Gizmos.DrawSphere(wp.position, 2f);

                // Draw lines connecting them
                if (i < _generatedWaypoints.Count - 1)
                    Gizmos.DrawLine(_generatedWaypoints[i].position, _generatedWaypoints[i + 1].position);
            }
        }
    }

    private void GenerateWaypoints()
    {
        // Create a parent object to keep the hierarchy clean
        var waypointParent = new GameObject("Patrol_Waypoints");
        waypointParent.transform.position = transform.position;

        for (var i = 0; i < waypointCount; i++)
        {
            var randomPos = GetValidRandomPosition();

            // Create a simple empty game object as a waypoint
            var wp = new GameObject($"Waypoint_{i}");
            wp.transform.SetParent(waypointParent.transform);
            wp.transform.position = randomPos;

            _generatedWaypoints.Add(wp.transform);
        }
    }

    private Vector3 GetValidRandomPosition()
    {
        // Try to find a position that isn't too close to the last one
        // to prevent impossible sharp turns
        var candidate = Vector3.zero;
        var valid = false;
        var attempts = 0;

        while (!valid && attempts < 10)
        {
            var randomPoint = new Vector3(
                Random.Range(-spawnAreaSize.x / 2, spawnAreaSize.x / 2),
                Random.Range(-spawnAreaSize.y / 2, spawnAreaSize.y / 2),
                Random.Range(-spawnAreaSize.z / 2, spawnAreaSize.z / 2)
            );

            candidate = transform.position + randomPoint + Vector3.up * heightOffset;

            // Check distance against the last added waypoint
            if (_generatedWaypoints.Count > 0)
            {
                var dist = Vector3.Distance(candidate, _generatedWaypoints[_generatedWaypoints.Count - 1].position);
                if (dist >= minWaypointDistance) valid = true;
            }
            else
            {
                valid = true;
            }

            attempts++;
        }

        return candidate;
    }

    private void SpawnJets()
    {
        if (jetPrefab == null) return;

        for (var i = 0; i < jetCount; i++)
        {
            // Spawn inside the box, but randomized so they don't explode on start
            var startPos = _generatedWaypoints[0].position + Random.insideUnitSphere * 15f;
            var startRot = Quaternion.LookRotation(_generatedWaypoints[1].position - startPos);

            var jet = Instantiate(jetPrefab, startPos, startRot);

            // Initialize the AI
            var ai = jet.GetComponent<FighterJetAi>();
            if (ai != null)
                // We pass the full list of waypoints
                ai.Initialize(_generatedWaypoints);
        }
    }
}