using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class EventEnemyArrivalMover : MonoBehaviour
{
    private Rigidbody2D rb;
    private EnemyBaseAI enemyAI;
    private EnemyAttackController attackController;
    private EnemyRoleController roleController;
    private EnemyRoleSimulationGate roleSimulationGate;
    private EnemyVisionSensor visionSensor;
    private Collider2D[] colliders;
    private bool[] colliderEnabledStates;
    private SpriteRenderer[] sourceRenderers;

    private Coroutine arrivalRoutine;
    private Vector2 entryPosition;
    private Vector2 arrivalPosition;
    private Transform target;
    private bool alertWhenReady;
    private float warningTime;
    private float travelTime;
    private float readyDelay;
    private GameObject landingEffectPrefab;
    private bool createFallbackLandingBurst;
    private Color landingBurstColor;
    private float landingBurstRadius;
    private bool useAfterimages;
    private float afterimageInterval;
    private float afterimageLifetime;
    private Color afterimageColor;
    private int afterimageSortingOrderOffset;
    private bool disableCollidersDuringArrival;
    private bool aiWasEnabled;
    private bool attackWasEnabled;
    private bool roleWasEnabled;
    private bool roleSimulationWasEnabled;
    private bool visionWasEnabled;
    private bool arrivalControlSuppressed;
    private float afterimageTimer;

    [Header("Arrival Facing")]
    [SerializeField] private bool rotateToArrivalDirection = true;
    [SerializeField] private float rotationOffset = -90f;

    public void Begin(
        Vector2 entry,
        Vector2 arrival,
        Transform targetTransform,
        bool alertReady,
        float warningDuration,
        float arrivalTravelDuration,
        float delayAfterArrival,
        GameObject impactPrefab,
        bool fallbackLandingBurst,
        Color fallbackBurstColor,
        float fallbackBurstRadius,
        bool spawnAfterimages,
        float ghostInterval,
        float ghostLifetime,
        Color ghostColor,
        int ghostSortingOrderOffset,
        bool disableColliders)
    {
        entryPosition = entry;
        arrivalPosition = arrival;
        target = targetTransform;
        alertWhenReady = alertReady;
        warningTime = Mathf.Max(0f, warningDuration);
        travelTime = Mathf.Max(0.01f, arrivalTravelDuration);
        readyDelay = Mathf.Max(0f, delayAfterArrival);
        landingEffectPrefab = impactPrefab;
        createFallbackLandingBurst = fallbackLandingBurst;
        landingBurstColor = fallbackBurstColor;
        landingBurstRadius = Mathf.Max(0.05f, fallbackBurstRadius);
        useAfterimages = spawnAfterimages;
        afterimageInterval = Mathf.Max(0.01f, ghostInterval);
        afterimageLifetime = Mathf.Max(0.01f, ghostLifetime);
        afterimageColor = ghostColor;
        afterimageSortingOrderOffset = ghostSortingOrderOffset;
        disableCollidersDuringArrival = disableColliders;

        CacheReferences();
        CacheSourceRenderers();
        CacheColliderStates();

        if (arrivalRoutine != null)
        {
            StopCoroutine(arrivalRoutine);
        }

        arrivalRoutine = StartCoroutine(ArrivalRoutine());
    }

    private void CacheReferences()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody2D>();
        }

        if (enemyAI == null)
        {
            enemyAI = GetComponentInChildren<EnemyBaseAI>(true);
        }

        if (attackController == null)
        {
            attackController = GetComponentInChildren<EnemyAttackController>(true);
        }

        if (roleController == null)
        {
            roleController = GetComponentInChildren<EnemyRoleController>(true);
        }

        if (roleSimulationGate == null)
        {
            roleSimulationGate = GetComponentInChildren<EnemyRoleSimulationGate>(true);
        }

        if (visionSensor == null)
        {
            visionSensor = GetComponentInChildren<EnemyVisionSensor>(true);
        }

        if (colliders == null || colliders.Length == 0)
        {
            colliders = GetComponentsInChildren<Collider2D>(true);
        }
    }

    private void CacheSourceRenderers()
    {
        sourceRenderers = GetComponentsInChildren<SpriteRenderer>(true);
    }

    private void CacheColliderStates()
    {
        if (colliders == null)
        {
            colliderEnabledStates = null;
            return;
        }

        colliderEnabledStates = new bool[colliders.Length];

        for (int i = 0; i < colliders.Length; i++)
        {
            colliderEnabledStates[i] = colliders[i] != null && colliders[i].enabled;
        }
    }

    private IEnumerator ArrivalRoutine()
    {
        transform.position = entryPosition;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.position = entryPosition;
        }

        SetFacing(arrivalPosition - entryPosition);
        DisableControlForArrival();

        if (warningTime > 0f)
        {
            yield return new WaitForSeconds(warningTime);
        }

        float timer = 0f;
        afterimageTimer = 0f;

        while (timer < travelTime)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / travelTime);
            float easedT = EaseOutCubic(t);
            Vector2 position = Vector2.Lerp(entryPosition, arrivalPosition, easedT);

            MoveTo(position);
            SetFacing(arrivalPosition - (Vector2)transform.position);
            TickAfterimages();

            yield return null;
        }

        MoveTo(arrivalPosition);
        SpawnLandingEffect();
        RestoreCollidersAfterArrival();

        if (readyDelay > 0f)
        {
            yield return new WaitForSeconds(readyDelay);
        }

        EnableControlAfterArrival();
        AlertIfNeeded();
        arrivalRoutine = null;
    }

    private void DisableControlForArrival()
    {
        arrivalControlSuppressed = true;

        if (enemyAI != null)
        {
            aiWasEnabled = enemyAI.enabled;
            enemyAI.enabled = false;
        }

        if (attackController != null)
        {
            attackWasEnabled = attackController.enabled;
            attackController.enabled = false;
        }

        if (roleController != null)
        {
            roleWasEnabled = roleController.enabled;
            roleController.enabled = false;
        }

        if (roleSimulationGate != null)
        {
            roleSimulationWasEnabled = roleSimulationGate.enabled;
            roleSimulationGate.enabled = false;
        }

        if (visionSensor != null)
        {
            visionWasEnabled = visionSensor.enabled;
            visionSensor.enabled = false;
        }

        if (disableCollidersDuringArrival)
        {
            SetCollidersEnabled(false);
        }
    }

    private void EnableControlAfterArrival()
    {
        if (visionSensor != null)
        {
            visionSensor.enabled = visionWasEnabled;
        }

        if (roleSimulationGate != null)
        {
            roleSimulationGate.enabled = roleSimulationWasEnabled;
        }

        if (roleController != null)
        {
            roleController.enabled = roleWasEnabled;
        }

        if (attackController != null)
        {
            attackController.enabled = attackWasEnabled;
        }

        if (enemyAI != null)
        {
            enemyAI.enabled = aiWasEnabled;
        }

        arrivalControlSuppressed = false;
    }

    private void RestoreCollidersAfterArrival()
    {
        if (!disableCollidersDuringArrival || colliders == null)
        {
            return;
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] == null)
            {
                continue;
            }

            bool previousState = colliderEnabledStates != null &&
                                 i < colliderEnabledStates.Length &&
                                 colliderEnabledStates[i];

            colliders[i].enabled = previousState;
        }
    }

    private void SetCollidersEnabled(bool value)
    {
        if (colliders == null)
        {
            return;
        }

        foreach (Collider2D targetCollider in colliders)
        {
            if (targetCollider != null)
            {
                targetCollider.enabled = value;
            }
        }
    }

    private void MoveTo(Vector2 position)
    {
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.position = position;
        }

        transform.position = position;
    }

    private void SetFacing(Vector2 direction)
    {
        if (!rotateToArrivalDirection)
        {
            return;
        }

        if (direction.sqrMagnitude <= 0.001f)
        {
            return;
        }

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle + rotationOffset);
    }
    private void AlertIfNeeded()
    {
        if (!alertWhenReady || enemyAI == null)
        {
            return;
        }

        if (target != null)
        {
            enemyAI.SetTarget(target);
            enemyAI.AlertTo(target.position);
        }
        else
        {
            enemyAI.AlertTo(arrivalPosition);
        }
    }

    private void SpawnLandingEffect()
    {
        if (landingEffectPrefab != null)
        {
            Instantiate(landingEffectPrefab, arrivalPosition, Quaternion.identity);
            return;
        }

        if (createFallbackLandingBurst)
        {
            EventEnemyArrivalMarker.SpawnImpact(arrivalPosition, landingBurstColor, landingBurstRadius, 0.22f);
        }
    }

    private void TickAfterimages()
    {
        if (!useAfterimages)
        {
            return;
        }

        afterimageTimer -= Time.deltaTime;

        if (afterimageTimer > 0f)
        {
            return;
        }

        afterimageTimer = afterimageInterval;
        SpawnAfterimage();
    }

    private void SpawnAfterimage()
    {
        if (sourceRenderers == null || sourceRenderers.Length == 0)
        {
            CacheSourceRenderers();
        }

        if (sourceRenderers == null || sourceRenderers.Length == 0)
        {
            return;
        }

        GameObject root = new GameObject("EnemyArrivalAfterimage");
        root.transform.position = Vector3.zero;
        root.transform.rotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        bool copiedAny = false;

        foreach (SpriteRenderer source in sourceRenderers)
        {
            if (source == null)
            {
                continue;
            }

            if (!source.enabled || !source.gameObject.activeInHierarchy || source.sprite == null)
            {
                continue;
            }

            GameObject child = new GameObject($"{source.name}_Ghost");
            child.transform.SetParent(root.transform, true);
            child.transform.position = source.transform.position;
            child.transform.rotation = source.transform.rotation;
            child.transform.localScale = source.transform.lossyScale;

            SpriteRenderer ghost = child.AddComponent<SpriteRenderer>();
            ghost.sprite = source.sprite;
            ghost.flipX = source.flipX;
            ghost.flipY = source.flipY;
            ghost.drawMode = source.drawMode;
            ghost.size = source.size;
            ghost.maskInteraction = source.maskInteraction;
            ghost.sortingLayerID = source.sortingLayerID;
            ghost.sortingOrder = source.sortingOrder + afterimageSortingOrderOffset;
            ghost.color = afterimageColor;

            copiedAny = true;
        }

        if (!copiedAny)
        {
            Destroy(root);
            return;
        }

        SpriteAfterimageGhost ghostController = root.AddComponent<SpriteAfterimageGhost>();
        ghostController.Initialize(afterimageLifetime);
    }


    private void OnDisable()
    {
        if (arrivalRoutine != null)
        {
            StopCoroutine(arrivalRoutine);
            arrivalRoutine = null;
        }

        if (!arrivalControlSuppressed)
        {
            return;
        }

        RestoreCollidersAfterArrival();
        EnableControlAfterArrival();
    }

    private float EaseOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        float oneMinusT = 1f - t;
        return 1f - oneMinusT * oneMinusT * oneMinusT;
    }
}
