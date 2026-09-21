using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BossDeathPresentation : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private Rigidbody2D bossRigidbody;

    [Header("Timing")]
    [Min(0.05f)]
    [SerializeField] private float destabilizeDuration = 0.45f;
    [Min(0.05f)]
    [SerializeField] private float breakupHoldDuration = 0.7f;
    [Min(0f)]
    [SerializeField] private float finalEmphasisDelay = 0.32f;

    [Header("Destabilize")]
    [SerializeField] private float positionJitter = 0.055f;
    [SerializeField] private float rotationJitter = 3f;
    [SerializeField] private Color destabilizeColor = new Color(1f, 0.35f, 0.82f, 1f);

    [Header("Breakup")]
    [SerializeField] private int breakupColumns = 3;
    [SerializeField] private int breakupRows = 2;
    [SerializeField] private float fragmentMinSpeed = 1.8f;
    [SerializeField] private float fragmentMaxSpeed = 3.8f;
    [SerializeField] private float fragmentAngularSpeed = 240f;
    [SerializeField] private float fragmentLifetime = 1.35f;

    [Header("Feedback")]
    [SerializeField] private float initialShakeAmplitude = 0.12f;
    [SerializeField] private float initialShakeDuration = 0.16f;
    [SerializeField] private float breakupShakeAmplitude = 0.2f;
    [SerializeField] private float breakupShakeDuration = 0.24f;

    private GungeonStyleCamera2D gameplayCamera;
    private PlayerController2D lockedPlayerController;
    private PlayerWeaponController lockedWeaponController;
    private bool presentationLocksHeld;
    private bool playing;
    private bool completed;
    private bool hasDeathPositionOverride;
    private Vector3 deathPositionOverride;

    public float RequiredBossLifetime =>
        Mathf.Max(0.05f, destabilizeDuration) +
        Mathf.Max(0.05f, breakupHoldDuration) +
        Mathf.Max(0f, finalEmphasisDelay) +
        0.35f;

    private void Reset()
    {
        bodyRenderer = GetComponentInChildren<SpriteRenderer>();
        bossRigidbody = GetComponent<Rigidbody2D>();
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        playing = false;
        completed = false;
        hasDeathPositionOverride = false;
        deathPositionOverride = Vector3.zero;

        if (bodyRenderer != null)
        {
            bodyRenderer.enabled = true;
        }
    }

    private void OnDisable()
    {
        CancelPresentation();
    }

    public void CancelPresentation()
    {
        ReleasePresentationLocks(true);
        playing = false;
        completed = true;
    }

    public void SetBodyRenderer(SpriteRenderer renderer)
    {
        if (playing || completed || renderer == null)
        {
            return;
        }

        bodyRenderer = renderer;
    }

    public void SetDeathPositionOverride(Vector3 worldPosition)
    {
        if (playing || completed)
        {
            return;
        }

        deathPositionOverride = worldPosition;
        hasDeathPositionOverride = true;
    }

    public Vector3 ResolvePresentationDeathPosition(Vector3 fallbackPosition)
    {
        return hasDeathPositionOverride
            ? deathPositionOverride
            : fallbackPosition;
    }

    public IEnumerator PlayRoutine(Vector3 deathPosition)
    {
        if (completed)
        {
            yield break;
        }

        if (playing)
        {
            while (playing)
            {
                yield return null;
            }

            yield break;
        }

        playing = true;
        ResolveReferences();
        Vector3 presentationPosition = hasDeathPositionOverride
            ? deathPositionOverride
            : deathPosition;
        AcquirePresentationLocks(presentationPosition);
        BossHealthBarUI.Instance?.Hide();

        Vector3 originalPosition = transform.position;
        Quaternion originalRotation = transform.rotation;
        Color originalColor = bodyRenderer != null ? bodyRenderer.color : Color.white;

        GungeonStyleCamera2D.RequestShake(initialShakeAmplitude, initialShakeDuration);
        yield return PlayDestabilizeRoutine(originalPosition, originalRotation, originalColor);

        transform.SetPositionAndRotation(originalPosition, originalRotation);

        if (bodyRenderer != null)
        {
            bodyRenderer.color = originalColor;
            SpawnBreakupFragments(bodyRenderer);
            bodyRenderer.enabled = false;
        }

        AudioManager.PlayAt(SoundEventIds.ShipDeathBreakup, presentationPosition);
        CombatFeedbackManager.PlayBreak(
            presentationPosition,
            CombatFeedbackKind.Boss,
            2.1f,
            breakupShakeAmplitude,
            breakupShakeDuration,
            true
        );

        yield return WaitUnscaled(breakupHoldDuration);

        CombatFeedbackManager.PlayBreak(
            presentationPosition,
            CombatFeedbackKind.Boss,
            1.45f,
            breakupShakeAmplitude * 0.65f,
            breakupShakeDuration * 0.8f,
            true
        );

        yield return WaitUnscaled(finalEmphasisDelay);

        ReleasePresentationLocks(false);
        playing = false;
        completed = true;
    }

    private IEnumerator PlayDestabilizeRoutine(
        Vector3 originalPosition,
        Quaternion originalRotation,
        Color originalColor)
    {
        float duration = Mathf.Max(0.05f, destabilizeDuration);
        float elapsed = 0f;
        int step = 0;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float normalized = Mathf.Clamp01(elapsed / duration);
            float intensity = Mathf.Sin(normalized * Mathf.PI);
            float horizontalSign = (step & 1) == 0 ? -1f : 1f;
            float verticalSign = (step & 2) == 0 ? -0.5f : 0.5f;

            transform.position = originalPosition + new Vector3(
                horizontalSign * positionJitter * intensity,
                verticalSign * positionJitter * intensity,
                0f
            );
            transform.rotation = originalRotation * Quaternion.Euler(
                0f,
                0f,
                horizontalSign * rotationJitter * intensity
            );

            if (bodyRenderer != null)
            {
                bodyRenderer.color = Color.Lerp(originalColor, destabilizeColor, intensity * 0.72f);
            }

            step++;
            yield return null;
        }
    }

    private void SpawnBreakupFragments(SpriteRenderer sourceRenderer)
    {
        Sprite sourceSprite = sourceRenderer.sprite;

        if (sourceSprite == null || sourceSprite.texture == null)
        {
            return;
        }

        int columns = Mathf.Clamp(breakupColumns, 2, 4);
        int rows = Mathf.Clamp(breakupRows, 2, 3);
        Rect textureRect;

        try
        {
            textureRect = sourceSprite.textureRect;
        }
        catch (UnityException)
        {
            return;
        }

        float pixelsPerUnit = Mathf.Max(1f, sourceSprite.pixelsPerUnit);
        Transform sourceTransform = sourceRenderer.transform;
        Vector3 sourceLossyScale = sourceTransform.lossyScale;
        Vector2 sourcePivot = sourceSprite.pivot;

        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                float xMin = Mathf.Round(textureRect.x + textureRect.width * column / columns);
                float xMax = Mathf.Round(textureRect.x + textureRect.width * (column + 1) / columns);
                float yMin = Mathf.Round(textureRect.y + textureRect.height * row / rows);
                float yMax = Mathf.Round(textureRect.y + textureRect.height * (row + 1) / rows);
                Rect pieceRect = new Rect(
                    xMin,
                    yMin,
                    Mathf.Max(1f, xMax - xMin),
                    Mathf.Max(1f, yMax - yMin)
                );
                Sprite pieceSprite = Sprite.Create(
                    sourceSprite.texture,
                    pieceRect,
                    new Vector2(0.5f, 0.5f),
                    pixelsPerUnit,
                    0u,
                    SpriteMeshType.FullRect
                );
                pieceSprite.name = $"{sourceSprite.name}_BossFragment_{column}_{row}";

                float localX = (pieceRect.center.x - textureRect.x - sourcePivot.x) / pixelsPerUnit;
                float localY = (pieceRect.center.y - textureRect.y - sourcePivot.y) / pixelsPerUnit;
                Vector3 worldPosition = sourceTransform.TransformPoint(new Vector3(localX, localY, 0f));

                GameObject fragment = new GameObject($"Boss Fragment {column + row * columns}");
                fragment.transform.SetPositionAndRotation(worldPosition, sourceTransform.rotation);
                fragment.transform.localScale = sourceLossyScale;

                SpriteRenderer renderer = fragment.AddComponent<SpriteRenderer>();
                renderer.sprite = pieceSprite;
                renderer.sharedMaterial = sourceRenderer.sharedMaterial;
                renderer.color = sourceRenderer.color;
                renderer.sortingLayerID = sourceRenderer.sortingLayerID;
                renderer.sortingOrder = sourceRenderer.sortingOrder + 1;

                Rigidbody2D fragmentBody = fragment.AddComponent<Rigidbody2D>();
                fragmentBody.gravityScale = 0f;
                fragmentBody.linearDamping = 1.1f;
                fragmentBody.angularDamping = 0.2f;
                fragmentBody.interpolation = RigidbodyInterpolation2D.Interpolate;

                Vector2 outward = (Vector2)(worldPosition - sourceTransform.position);

                if (outward.sqrMagnitude <= 0.001f)
                {
                    outward = Random.insideUnitCircle;
                }

                outward.Normalize();
                fragmentBody.linearVelocity = outward * Random.Range(fragmentMinSpeed, fragmentMaxSpeed);
                fragmentBody.angularVelocity = Random.Range(-fragmentAngularSpeed, fragmentAngularSpeed);

                Destroy(fragment, Mathf.Max(0.2f, fragmentLifetime));
                Destroy(pieceSprite, Mathf.Max(0.25f, fragmentLifetime + 0.05f));
            }
        }
    }

    private void ResolveReferences()
    {
        if (bodyRenderer == null)
        {
            bodyRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        if (bossRigidbody == null)
        {
            bossRigidbody = GetComponent<Rigidbody2D>();
        }

        if (gameplayCamera == null)
        {
            gameplayCamera = GungeonStyleCamera2D.Instance;
        }
    }

    private void AcquirePresentationLocks(Vector3 focusPosition)
    {
        if (presentationLocksHeld)
        {
            return;
        }

        presentationLocksHeld = true;

        if (bossRigidbody != null)
        {
            bossRigidbody.linearVelocity = Vector2.zero;
            bossRigidbody.angularVelocity = 0f;
        }

        if (gameplayCamera != null)
        {
            gameplayCamera.SetCinematicInputOffsetLocked(this, true);
            gameplayCamera.TryBeginOwnedCinematicFocusBlend(this, focusPosition, 0f, null, out _);
        }

        LockPlayerInput(FindFirstObjectByType<PlayerController2D>());
    }

    private void LockPlayerInput(PlayerController2D player)
    {
        lockedPlayerController = player;

        if (lockedPlayerController != null)
        {
            lockedPlayerController.GetComponent<PlayerDash>()?.CancelActiveDash();
            lockedPlayerController.SetExternalControlLocked(this, true);
            lockedWeaponController = lockedPlayerController.GetComponent<PlayerWeaponController>();
        }

        if (lockedWeaponController != null)
        {
            lockedWeaponController.SetExternalInputLocked(this, true);
        }
    }

    private void ReleasePresentationLocks(bool resetCamera)
    {
        if (!presentationLocksHeld)
        {
            return;
        }

        if (gameplayCamera != null)
        {
            gameplayCamera.ReleaseOwnedCinematicFocus(this, resetCamera);
            gameplayCamera.SetCinematicInputOffsetLocked(this, false);
        }

        if (lockedPlayerController != null)
        {
            lockedPlayerController.SetExternalControlLocked(this, false);
        }

        if (lockedWeaponController != null)
        {
            lockedWeaponController.SetExternalInputLocked(this, false);
        }

        lockedPlayerController = null;
        lockedWeaponController = null;
        presentationLocksHeld = false;
    }

    private static IEnumerator WaitUnscaled(float duration)
    {
        float remaining = Mathf.Max(0f, duration);

        while (remaining > 0f)
        {
            remaining -= Time.unscaledDeltaTime;
            yield return null;
        }
    }
}
