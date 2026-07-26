using System;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerShipVisualController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("실제 플레이어 기체를 표시하는 SpriteRenderer입니다.")]
    [SerializeField] private SpriteRenderer targetSpriteRenderer;
    [SerializeField] private PlayerWeaponController weaponController;
    [Tooltip("런타임에서 ShipDefinition을 전달받지 못했을 때 사용할 기본 기체 데이터입니다.")]
    [SerializeField] private ShipDefinition defaultShipDefinition;

    [Header("Options")]
    [SerializeField] private bool applyOnEnable = true;
    [Tooltip("기존 SpriteRenderer 오브젝트에 Animator가 남아 있으면 비활성화합니다.")]
    [SerializeField] private bool disableLegacyAnimatorOnSpriteObject = true;
    [SerializeField] private bool logMissingSprite = true;

    private ShipDefinition currentShipDefinition;
    private Sprite initialSprite;
    private WeaponTreeType currentWeaponTree = WeaponTreeType.MachineGun;

    public SpriteRenderer TargetSpriteRenderer => targetSpriteRenderer;
    public ShipDefinition CurrentShipDefinition => currentShipDefinition != null ? currentShipDefinition : defaultShipDefinition;
    public WeaponTreeType CurrentWeaponTree => currentWeaponTree;
    public Sprite CurrentSprite => targetSpriteRenderer != null ? targetSpriteRenderer.sprite : null;

    public event Action<WeaponTreeType, Sprite> VisualChanged;

    private void Reset()
    {
        CacheReferences();
    }

    private void Awake()
    {
        CacheReferences();
        currentShipDefinition = defaultShipDefinition;

        if (targetSpriteRenderer != null)
        {
            initialSprite = targetSpriteRenderer.sprite;
        }

        DisableLegacyAnimator();
    }

    private void OnEnable()
    {
        CacheReferences();

        if (weaponController != null)
        {
            weaponController.WeaponEquipped += HandleWeaponEquipped;
        }

        if (applyOnEnable)
        {
            ApplyCurrentVisual();
        }
    }

    private void Start()
    {
        ApplyCurrentVisual();
    }

    private void OnDisable()
    {
        if (weaponController != null)
        {
            weaponController.WeaponEquipped -= HandleWeaponEquipped;
        }
    }

    private void CacheReferences()
    {
        if (targetSpriteRenderer == null)
        {
            targetSpriteRenderer = FindBestSpriteRenderer();
        }

        if (weaponController == null)
        {
            weaponController = GetComponent<PlayerWeaponController>();

            if (weaponController == null)
            {
                weaponController = GetComponentInParent<PlayerWeaponController>();
            }
        }
    }


    private SpriteRenderer FindBestSpriteRenderer()
    {
        SpriteRenderer ownRenderer = GetComponent<SpriteRenderer>();
        if (IsPreferredRenderer(ownRenderer))
        {
            return ownRenderer;
        }

        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            if (IsPreferredRenderer(renderers[i]))
            {
                return renderers[i];
            }
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer != null && renderer.sprite != null)
            {
                return renderer;
            }
        }

        return renderers.Length > 0 ? renderers[0] : null;
    }

    private bool IsPreferredRenderer(SpriteRenderer renderer)
    {
        if (renderer == null || renderer.sprite == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
        {
            return false;
        }

        string objectName = renderer.gameObject.name.ToLowerInvariant();

        return !objectName.Contains("death") &&
               !objectName.Contains("part") &&
               !objectName.Contains("afterimage") &&
               !objectName.Contains("trail") &&
               !objectName.Contains("shadow") &&
               !objectName.Contains("vfx");
    }

    private void DisableLegacyAnimator()
    {
        if (!disableLegacyAnimatorOnSpriteObject || targetSpriteRenderer == null)
        {
            return;
        }

        Animator animator = targetSpriteRenderer.GetComponent<Animator>();
        if (animator != null)
        {
            animator.enabled = false;
        }
    }

    private void HandleWeaponEquipped(WeaponTreeType weaponTreeType, PlayerWeaponBase weapon)
    {
        ApplyVisual(weaponTreeType);
    }

    public void SetShipDefinition(ShipDefinition shipDefinition, bool refreshVisual = true)
    {
        currentShipDefinition = shipDefinition != null ? shipDefinition : defaultShipDefinition;

        if (refreshVisual)
        {
            ApplyCurrentVisual();
        }
    }

    public void ApplyCurrentVisual()
    {
        WeaponTreeType targetTree = ResolveCurrentWeaponTree();
        ApplyVisual(targetTree);
    }

    public void ApplyVisual(WeaponTreeType weaponTreeType)
    {
        CacheReferences();
        DisableLegacyAnimator();

        currentWeaponTree = weaponTreeType;

        if (targetSpriteRenderer == null)
        {
            if (logMissingSprite)
            {
                Debug.LogWarning("PlayerShipVisualController의 Target Sprite Renderer가 연결되지 않았습니다.", this);
            }

            return;
        }

        Sprite targetSprite = GetSprite(weaponTreeType);

        if (targetSprite == null)
        {
            if (logMissingSprite)
            {
                Debug.LogWarning($"{weaponTreeType} 기체 스프라이트가 연결되지 않았습니다.", this);
            }

            return;
        }

        if (targetSpriteRenderer.sprite != targetSprite)
        {
            targetSpriteRenderer.sprite = targetSprite;
        }

        VisualChanged?.Invoke(weaponTreeType, targetSprite);
    }

    public Sprite GetSprite(WeaponTreeType weaponTreeType)
    {
        ShipDefinition definition = CurrentShipDefinition;
        Sprite sprite = definition != null ? definition.GetWeaponSprite(weaponTreeType) : null;

        if (sprite != null)
        {
            return sprite;
        }

        return initialSprite;
    }

    private WeaponTreeType ResolveCurrentWeaponTree()
    {
        if (weaponController != null && weaponController.CurrentWeapon != null)
        {
            return weaponController.CurrentWeaponTree;
        }

        if (RunManager.Instance != null && RunManager.Instance.HasActiveRun)
        {
            return RunManager.Instance.CurrentRun.SelectedWeaponTree;
        }

        if (PermanentProgress.Instance != null)
        {
            return PermanentProgress.Instance.LastSelectedWeaponTree;
        }

        return CurrentShipDefinition != null
            ? CurrentShipDefinition.DefaultWeaponTree
            : WeaponTreeType.MachineGun;
    }
}
