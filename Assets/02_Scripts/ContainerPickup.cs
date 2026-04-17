using System;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class ContainerPickup : MonoBehaviour
{
    public static event Action<int> Collected;

    [Header("Container Settings")]
    [SerializeField] private int rewardAmount = 1;
    [SerializeField] private float lifeTime = 10f;
    [SerializeField] private float magnetRange = 1.5f;
    [SerializeField] private float magnetSpeed = 6f;
    [SerializeField] private float spinSpeed = 90f;
    [SerializeField] private string playerTag = "Player";

    private float lifeTimer;
    private Transform player;

    private void OnEnable()
    {
        lifeTimer = lifeTime;
        FindPlayer();
    }

    private void Update()
    {
        lifeTimer -= Time.deltaTime;
        if (lifeTimer <= 0f)
        {
            ReleaseSelf();
            return;
        }

        transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);

        if (player == null || !player.gameObject.activeInHierarchy)
        {
            FindPlayer();
            return;
        }

        PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
        if (playerHealth != null && playerHealth.IsDead)
        {
            return;
        }

        float distance = Vector2.Distance(transform.position, player.position);
        if (distance <= magnetRange)
        {
            transform.position = Vector2.MoveTowards(transform.position, player.position, magnetSpeed * Time.deltaTime);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
        if (playerHealth == null || playerHealth.IsDead)
        {
            return;
        }

        Collected?.Invoke(rewardAmount);
        ReleaseSelf();
    }

    private void FindPlayer()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag(playerTag);
        if (playerObject != null)
        {
            player = playerObject.transform;
        }
    }

    private void ReleaseSelf()
    {
        if (PoolManager.Instance != null)
        {
            PoolManager.Instance.Release(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
