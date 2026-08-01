using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PhaseAfterimageDecoy : MonoBehaviour
{
    private readonly List<EnemyBaseAI> affectedEnemies = new List<EnemyBaseAI>();
    private readonly List<Transform> previousTargets = new List<Transform>();

    private SpriteRenderer spriteRenderer;
    private float lifetime;
    private Color startColor;

    public void Initialize(
        Transform player,
        SpriteRenderer sourceRenderer,
        Color color,
        float duration,
        float tauntRadius)
    {
        lifetime = Mathf.Max(0.1f, duration);
        startColor = color;

        spriteRenderer = gameObject.AddComponent<SpriteRenderer>();

        if (sourceRenderer != null)
        {
            spriteRenderer.sprite = sourceRenderer.sprite;
            spriteRenderer.flipX = sourceRenderer.flipX;
            spriteRenderer.flipY = sourceRenderer.flipY;
            spriteRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
            spriteRenderer.sortingOrder = sourceRenderer.sortingOrder - 1;
            transform.rotation = sourceRenderer.transform.rotation;
            transform.localScale = sourceRenderer.transform.lossyScale;
        }

        spriteRenderer.color = startColor;
        RedirectEnemies(player, tauntRadius);
        StartCoroutine(LifetimeRoutine());
    }

    private void RedirectEnemies(Transform player, float tauntRadius)
    {
        EnemyBaseAI[] enemies = FindObjectsByType<EnemyBaseAI>(FindObjectsSortMode.None);
        float sqrRadius = Mathf.Max(0f, tauntRadius) * Mathf.Max(0f, tauntRadius);

        for (int i = 0; i < enemies.Length; i++)
        {
            EnemyBaseAI enemy = enemies[i];

            if (enemy == null || enemy.CurrentState == EnemyState.Dead)
            {
                continue;
            }

            if (((Vector2)enemy.transform.position - (Vector2)transform.position).sqrMagnitude > sqrRadius)
            {
                continue;
            }

            Transform previousTarget = enemy.Player != null ? enemy.Player : player;
            affectedEnemies.Add(enemy);
            previousTargets.Add(previousTarget);
            enemy.SetTarget(transform);
            enemy.AlertTo(transform.position);
        }
    }

    private IEnumerator LifetimeRoutine()
    {
        float timer = 0f;

        while (timer < lifetime)
        {
            timer += Time.deltaTime;

            if (spriteRenderer != null)
            {
                Color color = startColor;
                color.a = startColor.a * (1f - Mathf.Clamp01(timer / lifetime));
                spriteRenderer.color = color;
            }

            yield return null;
        }

        RestoreTargets();
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        RestoreTargets();
    }

    private void RestoreTargets()
    {
        for (int i = 0; i < affectedEnemies.Count; i++)
        {
            EnemyBaseAI enemy = affectedEnemies[i];

            if (enemy == null || enemy.Player != transform)
            {
                continue;
            }

            Transform target = i < previousTargets.Count ? previousTargets[i] : null;
            enemy.SetTarget(target);
        }

        affectedEnemies.Clear();
        previousTargets.Clear();
    }
}
