using System.Collections.Generic;
using UnityEngine;

public class TrackManager : MonoBehaviour
{
    [Header("Starting Reference")]
    [Tooltip("Point where the track starts generating forward")]
    [SerializeField] private Transform trackStartPoint;

    [Header("Prefab (Optional)")]
    [Tooltip("Leave empty to auto-create sphere triggers")]
    [SerializeField] private GameObject checkpointPrefab;

    [Header("Track Shape & Spacing")]
    [Tooltip("Minimum distance between consecutive checkpoints")]
    [SerializeField] private float minDistance = 10f;

    [Tooltip("Maximum distance between consecutive checkpoints")]
    [SerializeField] private float maxDistance = 18f;

    [Tooltip("Maximum turn angle (degrees) between checkpoints")]
    [Range(5f, 90f)]
    [SerializeField] private float maxTurnAngle = 45f;

    [Tooltip("Height above ground to spawn checkpoints")]
    [SerializeField] private float spawnHeight = 0.5f;

    [Tooltip("Radius/scale of generated spheres")]
    [SerializeField] private float sphereScale = 2f;

    [Header("Buffer & Cleanup")]
    [Tooltip("Checkpoints spawned on start")]
    [SerializeField] private int initialCount = 15;

    [Tooltip("How many checkpoints to keep ahead of the leading car")]
    [SerializeField] private int lookaheadBuffer = 8;

    [Tooltip("How many checkpoints behind the lowest car before destroying")]
    [SerializeField] private int tailCleanupBuffer = 10;

    [Header("Ghost Cars (No collision)")]
    [Tooltip("Automatically disable collisions between objects on the Car layer")]
    [SerializeField] private bool disableCarCollisions = true;
    [SerializeField] private string carLayerName = "Car";

    [Header("Colors (Based on First/Lead Car)")]
    [Tooltip("Color of the leader car's current target checkpoint")]
    [SerializeField] private Color leaderCurrentColor = Color.green;

    [Tooltip("Color of the leader car's next target checkpoint")]
    [SerializeField] private Color leaderNextColor = Color.yellow;

    [Tooltip("Color of upcoming checkpoints ahead of the leader")]
    [SerializeField] private Color upcomingColor = new Color(1f, 1f, 1f, 0.4f);

    private readonly Dictionary<int, Checkpoint> activeCheckpoints = new Dictionary<int, Checkpoint>();
    private readonly List<CarAgent> activeCars = new List<CarAgent>();
    private Vector3 lastSpawnPosition;
    private Vector3 lastSpawnDirection;
    private int highestSpawnedIndex = -1;
    private Transform leadingCar;
    private int leadingIndex = 0;

    public Transform LeadingCar => leadingCar;
    public int LeadingIndex => leadingIndex;

    private void Awake()
    {
        if (disableCarCollisions)
        {
            int carLayer = LayerMask.NameToLayer(carLayerName);
            if (carLayer != -1)
            {
                Physics.IgnoreLayerCollision(carLayer, carLayer, true);
            }
        }
    }

    private void Start()
    {
        ResetTrack();
    }

    [ContextMenu("Reset Track")]
    public void ResetTrack()
    {
        foreach (var pair in activeCheckpoints)
        {
            if (pair.Value != null)
            {
                Destroy(pair.Value.gameObject);
            }
        }
        activeCheckpoints.Clear();
        highestSpawnedIndex = -1;

        Vector3 startPos = trackStartPoint != null ? trackStartPoint.position : transform.position;
        Vector3 startDir = trackStartPoint != null ? trackStartPoint.forward : transform.forward;
        startDir.y = 0;
        if (startDir.sqrMagnitude < 0.001f) startDir = Vector3.forward;
        startDir.Normalize();

        lastSpawnPosition = startPos;
        lastSpawnDirection = startDir;

        for (int i = 0; i < initialCount; i++)
        {
            SpawnNextCheckpoint();
        }

        leadingIndex = 0;
        leadingCar = null;
        UpdateColors();
    }

    /// <summary>
    /// Gets the world position of the checkpoint at the given index.
    /// Spawns ahead if needed.
    /// </summary>
    public Vector3 GetCheckpointPosition(int index)
    {
        EnsureCheckpointsUpTo(index);

        if (activeCheckpoints.TryGetValue(index, out var cp) && cp != null)
        {
            return cp.transform.position;
        }

        return lastSpawnPosition;
    }

    /// <summary>
    /// Gets the Transform of the checkpoint at index (if still active).
    /// </summary>
    public Transform GetCheckpointTransform(int index)
    {
        EnsureCheckpointsUpTo(index);

        if (activeCheckpoints.TryGetValue(index, out var cp) && cp != null)
        {
            return cp.transform;
        }

        return null;
    }

    /// <summary>
    /// Called by a car when it advances to ensure enough track is generated ahead.
    /// Updates the leader car reference and highlights targets.
    /// </summary>
    public void NotifyCarProgress(int currentCarIndex, Transform carTransform = null)
    {
        if (currentCarIndex >= leadingIndex)
        {
            leadingIndex = currentCarIndex;
            if (carTransform != null)
            {
                leadingCar = carTransform;
            }
            UpdateColors();
        }

        int targetIndex = currentCarIndex + lookaheadBuffer;
        EnsureCheckpointsUpTo(targetIndex);
    }

    public void RegisterCar(CarAgent car)
    {
        if (car != null && !activeCars.Contains(car))
        {
            activeCars.Add(car);
        }
    }

    public void UnregisterCar(CarAgent car)
    {
        if (car != null)
        {
            activeCars.Remove(car);
            if (leadingCar == car.transform)
            {
                RecalculateLeader();
            }
        }
    }

    public void NotifyCarReset(CarAgent car)
    {
        if (car != null && leadingCar == car.transform)
        {
            RecalculateLeader();
        }
    }

    public void RecalculateLeader()
    {
        CarAgent bestCar = null;
        int highestIndex = -1;

        for (int i = activeCars.Count - 1; i >= 0; i--)
        {
            if (activeCars[i] == null)
            {
                activeCars.RemoveAt(i);
                continue;
            }

            if (activeCars[i].CurrentCheckpointIndex > highestIndex)
            {
                highestIndex = activeCars[i].CurrentCheckpointIndex;
                bestCar = activeCars[i];
            }
        }

        if (bestCar != null)
        {
            leadingCar = bestCar.transform;
            leadingIndex = highestIndex;
        }
        else
        {
            leadingCar = null;
            leadingIndex = 0;
        }

        UpdateColors();
    }

    /// <summary>
    /// Colors the leader's current checkpoint green, next checkpoint yellow, and rest subtle.
    /// </summary>
    public void UpdateColors()
    {
        foreach (var kvp in activeCheckpoints)
        {
            if (kvp.Value == null) continue;

            if (kvp.Key == leadingIndex)
            {
                kvp.Value.SetColor(leaderCurrentColor);
            }
            else if (kvp.Key == leadingIndex + 1)
            {
                kvp.Value.SetColor(leaderNextColor);
            }
            else
            {
                kvp.Value.SetColor(upcomingColor);
            }
        }
    }

    /// <summary>
    /// Destroys old checkpoints behind the slowest car.
    /// </summary>
    public void CleanupBehind(int lowestActiveCarIndex)
    {
        int cleanupThreshold = lowestActiveCarIndex - tailCleanupBuffer;
        var toRemove = new List<int>();

        foreach (var kvp in activeCheckpoints)
        {
            if (kvp.Key < cleanupThreshold)
            {
                toRemove.Add(kvp.Key);
                if (kvp.Value != null)
                {
                    Destroy(kvp.Value.gameObject);
                }
            }
        }

        foreach (int key in toRemove)
        {
            activeCheckpoints.Remove(key);
        }
    }

    private void EnsureCheckpointsUpTo(int targetIndex)
    {
        while (highestSpawnedIndex < targetIndex)
        {
            SpawnNextCheckpoint();
        }
    }

    private void SpawnNextCheckpoint()
    {
        highestSpawnedIndex++;
        int newIndex = highestSpawnedIndex;

        float angle = Random.Range(-maxTurnAngle, maxTurnAngle);
        Quaternion rot = Quaternion.Euler(0f, angle, 0f);
        Vector3 newDir = rot * lastSpawnDirection;
        float dist = Random.Range(minDistance, maxDistance);

        Vector3 newPos = lastSpawnPosition + newDir.normalized * dist;
        newPos.y = spawnHeight;

        GameObject cpObj;
        if (checkpointPrefab != null)
        {
            cpObj = Instantiate(checkpointPrefab, newPos, Quaternion.identity, transform);
        }
        else
        {
            cpObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cpObj.transform.position = newPos;
            cpObj.transform.localScale = Vector3.one * sphereScale;
            cpObj.transform.SetParent(transform);
            var col = cpObj.GetComponent<SphereCollider>();
            if (col != null) col.isTrigger = true;
        }

        cpObj.name = $"Checkpoint_{newIndex}";

        Checkpoint cp = cpObj.GetComponent<Checkpoint>();
        if (cp == null) cp = cpObj.AddComponent<Checkpoint>();
        cp.Index = newIndex;
        cp.trackManager = this;

        activeCheckpoints[newIndex] = cp;

        if (newIndex == leadingIndex)
        {
            cp.SetColor(leaderCurrentColor);
        }
        else if (newIndex == leadingIndex + 1)
        {
            cp.SetColor(leaderNextColor);
        }
        else
        {
            cp.SetColor(upcomingColor);
        }

        lastSpawnPosition = newPos;
        lastSpawnDirection = newDir.normalized;
    }

    private void OnDrawGizmos()
    {
        if (activeCheckpoints.Count < 2) return;

        Gizmos.color = Color.cyan;
        Vector3? previousPos = null;

        for (int i = 0; i <= highestSpawnedIndex; i++)
        {
            if (activeCheckpoints.TryGetValue(i, out var cp) && cp != null)
            {
                Vector3 currentPos = cp.transform.position;
                if (previousPos.HasValue)
                {
                    Gizmos.DrawLine(previousPos.Value, currentPos);
                }
                previousPos = currentPos;
            }
        }
    }
}
