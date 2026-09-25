using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TrackManager trackManager;

    [Header("Isometric Follow Settings")]
    [Tooltip("Offset from target car (isometric style: back, up, side)")]
    [SerializeField] private Vector3 offset = new Vector3(-15f, 22f, -15f);

    [Tooltip("Camera position smoothing time (lower = snappier)")]
    [SerializeField] private float smoothTime = 0.2f;

    [Header("Rotation Settings")]
    [Tooltip("Keep rigid isometric angle without rotation wobbling")]
    [SerializeField] private bool fixedIsometricRotation = true;
    [SerializeField] private Vector3 isometricEulerAngles = new Vector3(45f, 45f, 0f);
    [SerializeField] private Vector3 lookAtOffset = new Vector3(0f, 0.5f, 0f);

    private Vector3 currentVelocity = Vector3.zero;
    private Transform currentTarget;

    private void Awake()
    {
        if (trackManager == null)
        {
            trackManager = FindAnyObjectByType<TrackManager>();
        }
    }

    private void LateUpdate()
    {
        UpdateTarget();

        if (currentTarget == null) return;

        // Smooth position follow
        Vector3 targetPosition = currentTarget.position + offset;
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref currentVelocity, smoothTime);

        // Apply rotation
        if (fixedIsometricRotation)
        {
            transform.rotation = Quaternion.Euler(isometricEulerAngles);
        }
        else
        {
            transform.LookAt(currentTarget.position + lookAtOffset);
        }
    }

    private void UpdateTarget()
    {
        // 1. Follow lead car from TrackManager
        if (trackManager != null && trackManager.LeadingCar != null)
        {
            currentTarget = trackManager.LeadingCar;
            return;
        }

        // 2. Fallback to any active car agent if lead not yet determined
        if (currentTarget == null)
        {
            CarAgent agent = FindAnyObjectByType<CarAgent>();
            if (agent != null)
            {
                currentTarget = agent.transform;
            }
        }
    }
}
