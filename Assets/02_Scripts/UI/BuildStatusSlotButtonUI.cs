using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class BuildStatusSlotButtonUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Button button;
    [SerializeField] private Image iconImage;
    [SerializeField] private Image rarityFrameImage;
    [SerializeField] private TextMeshProUGUI amountText;
    [SerializeField] private GameObject amountRoot;
    [SerializeField] private GameObject selectedRoot;
    [SerializeField] private GameObject ownedRoot;
    [SerializeField] private GameObject unownedRoot;
    [SerializeField] private Image unownedOverlayImage;

    [Header("Fallback")]
    [SerializeField] private Sprite fallbackIcon;
    [SerializeField] private Color fallbackRarityColor = Color.white;
    [SerializeField] private Color ownedIconColor = Color.white;
    [SerializeField] private Color unownedIconColor = new Color(0.35f, 0.35f, 0.35f, 0.7f);
    [SerializeField] private Color unownedOverlayColor = new Color(0f, 0f, 0f, 0.5f);

    private Action clicked;

    private void Reset()
    {
        button = GetComponent<Button>();

        if (iconImage == null)
        {
            iconImage = GetComponentInChildren<Image>(true);
        }
    }

    private void Awake()
    {
        CacheReferences();

        if (button != null)
        {
            button.onClick.AddListener(HandleClicked);
        }
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(HandleClicked);
        }
    }

    /// <summary>
    /// 기존 프리팹/호출부 호환용 단순 바인딩입니다.
    /// </summary>
    public void Bind(Sprite icon, Action onClicked)
    {
        Bind(icon, string.Empty, fallbackRarityColor, true, onClicked);
    }

    /// <summary>
    /// TAB 패시브 보관함 슬롯 바인딩입니다.
    /// </summary>
    public void Bind(
        Sprite icon,
        string amountLabel,
        Color rarityColor,
        bool owned,
        Action onClicked)
    {
        CacheReferences();

        clicked = onClicked;
        SetIcon(icon != null ? icon : fallbackIcon, owned);
        SetAmountLabel(amountLabel);
        SetRarityColor(rarityColor);
        SetOwned(owned);
        SetSelected(false);

        if (button != null)
        {
            button.interactable = true;
        }
    }

    public void SetSelected(bool selected)
    {
        if (selectedRoot != null)
        {
            selectedRoot.SetActive(selected);
        }
    }

    public void SetOwned(bool owned)
    {
        if (ownedRoot != null)
        {
            ownedRoot.SetActive(owned);
        }

        if (unownedRoot != null)
        {
            unownedRoot.SetActive(!owned);
        }

        if (iconImage != null)
        {
            iconImage.color = owned ? ownedIconColor : unownedIconColor;
        }

        if (unownedOverlayImage != null)
        {
            unownedOverlayImage.gameObject.SetActive(!owned);
            unownedOverlayImage.color = unownedOverlayColor;
        }
    }

    public void SetAmountLabel(string label)
    {
        bool visible = !string.IsNullOrWhiteSpace(label);

        if (amountRoot != null)
        {
            amountRoot.SetActive(visible);
        }

        if (amountText != null)
        {
            amountText.gameObject.SetActive(visible);
            amountText.text = visible ? label : string.Empty;
        }
    }

    public void SetRarityColor(Color color)
    {
        if (rarityFrameImage != null)
        {
            rarityFrameImage.color = color;
        }
    }

    private void CacheReferences()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }
    }

    private void SetIcon(Sprite icon, bool owned)
    {
        if (iconImage == null)
        {
            return;
        }

        iconImage.sprite = icon;
        iconImage.enabled = icon != null;
        iconImage.preserveAspect = true;
        iconImage.color = owned ? ownedIconColor : unownedIconColor;
    }

    private void HandleClicked()
    {
        clicked?.Invoke();
    }
}
