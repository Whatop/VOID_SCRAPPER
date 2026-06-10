using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class EventTitleTrigger2D : MonoBehaviour
{
    [Header("Title")]
    [SerializeField] private EventTitleType titleType = EventTitleType.CoreReaction;

    [Header("Trigger")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool triggerOnce = true;

    private bool triggered;

    private void Reset()
    {
        Collider2D col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (triggered && triggerOnce)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(playerTag) && !other.CompareTag(playerTag))
        {
            return;
        }

        triggered = true;

        if (EventTitleDirector.Instance != null)
        {
            EventTitleDirector.Instance.Show(titleType);
        }
    }

    public void ResetTrigger()
    {
        triggered = false;
    }
}