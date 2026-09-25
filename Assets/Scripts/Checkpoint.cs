using UnityEngine;

public interface ICheckpointListener
{
    void OnCheckpointReached(int checkpointIndex);
}

public class Checkpoint : MonoBehaviour
{
    [Tooltip("Order of this checkpoint along the track")]
    [SerializeField] private int index;

    public int Index
    {
        get => index;
        set => index = value;
    }

    [HideInInspector] public TrackManager trackManager;

    [SerializeField] private Renderer checkpointRenderer;

    public void SetColor(Color color)
    {
        if (checkpointRenderer == null) checkpointRenderer = GetComponent<Renderer>();
        if (checkpointRenderer != null) checkpointRenderer.material.color = color;
    }

    private void OnTriggerEnter(Collider other)
    {
        var listener = other.GetComponentInParent<ICheckpointListener>();
        if (listener != null)
        {
            listener.OnCheckpointReached(index);
        }
    }
}
