using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using UnityEngine.InputSystem;

public class CarAgent : Agent, ICheckpointListener
{
    [Header("Car Movement Settings")]
    [SerializeField] private float driveForce = 20f;
    [SerializeField] private float turnTorque = 100f;

    [Header("Track Reference")]
    [SerializeField] private TrackManager trackManager;

    [Header("Timer Settings")]
    [SerializeField] private float timePerCheckpoint = 10f;
    private float checkpointTimer;

    private Rigidbody rb;
    private int currentCheckpointIndex = 0;
    public int CurrentCheckpointIndex => currentCheckpointIndex;
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();
        if (trackManager == null)
        {
            trackManager = FindAnyObjectByType<TrackManager>();
        }

        spawnPosition = transform.position;
        spawnRotation = transform.rotation;
        trackManager?.RegisterCar(this);
    }

    private void OnDestroy()
    {
        trackManager?.UnregisterCar(this);
    }

    public override void OnEpisodeBegin()
    {
        // TODO: Reset car velocity, rotation, position, and currentCheckpointIndex
        currentCheckpointIndex = 0;
        trackManager?.NotifyCarReset(this);
        transform.position = spawnPosition;
        transform.rotation = spawnRotation;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        checkpointTimer = timePerCheckpoint;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // TODO: Tell AI brain what it sees:
        // - Where is current checkpoint relative to car?
        // - Where is next checkpoint relative to car?
        // - How fast is car going?
        sensor.AddObservation(transform.position);
        sensor.AddObservation(transform.rotation);
        sensor.AddObservation(rb.linearVelocity);
        sensor.AddObservation(rb.angularVelocity);
        // Get current Checkpoint and next Checkpoint
        Vector3 currentPos = trackManager.GetCheckpointPosition(currentCheckpointIndex);
        Vector3 nextPos = trackManager.GetCheckpointPosition(currentCheckpointIndex + 1);
        sensor.AddObservation(transform.InverseTransformPoint(currentPos));
        sensor.AddObservation(transform.InverseTransformPoint(nextPos));
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        // TODO: Read AI actions:
        // actions.ContinuousActions[0] -> Gas / Reverse (-1.0 to 1.0)
        // actions.ContinuousActions[1] -> Steer Left / Right (-1.0 to 1.0)
        // Apply physics force & torque
        // Reward or penalize agent
        
        Vector3 currentPos = trackManager.GetCheckpointPosition(currentCheckpointIndex);
        Vector3 toCheckpoint = (currentPos - transform.position).normalized;
        float forwardSpeed = Vector3.Dot(rb.linearVelocity, toCheckpoint);
        AddReward(forwardSpeed * 0.001f);
        
        if (transform.position.y < -5f)
        {
            AddReward(-1.0f);
            EndEpisode();
            return;
        }
        
        checkpointTimer -= Time.fixedDeltaTime;
        if (checkpointTimer <= 0f)
        {
            AddReward(-1.0f);
            EndEpisode();
            return;
        }
        
        // Simulate Gas/Brake
        rb.AddForce(transform.forward * driveForce * actions.ContinuousActions[0], ForceMode.Acceleration);
        // Simulate Turning
        rb.AddTorque(transform.up * actions.ContinuousActions[1] * turnTorque, ForceMode.Acceleration);
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // TODO: Let human drive with WASD/Arrows to test car physics
        var continuousActions = actionsOut.ContinuousActions;
        var kb = Keyboard.current;
        if (kb != null)
        {
            continuousActions[0] = (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed || kb.downArrowKey.
                isPressed ? 1f : 0f);
            continuousActions[1] = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed || kb.leftArrowKey.
                isPressed ? 1f : 0f);
        }
    }

    public void OnCheckpointReached(int checkpointIndex)
    {
        // TODO: Validate correct checkpoint, give reward, advance index
        if (currentCheckpointIndex == checkpointIndex)
        {
            AddReward(1f);
            checkpointTimer = timePerCheckpoint;
            currentCheckpointIndex++;
            trackManager.NotifyCarProgress(currentCheckpointIndex, transform);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        // TODO: Penalize if hitting walls or falling, not planned though
    }
}
