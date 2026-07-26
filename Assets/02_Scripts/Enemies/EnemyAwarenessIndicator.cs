using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class EnemyAwarenessIndicator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnemyBaseAI enemyAI;
    [SerializeField] private EnemyVisionSensor visionSensor;
    [SerializeField] private Transform anchor;
    [SerializeField] private TextMeshPro label;

    [Header("Auto Create")]
    [SerializeField] private bool autoCreateLabel = true;
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.15f, 0f);
    [SerializeField] private float fontSize = 4f;
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder = 100;

    [Header("Display")]
    [SerializeField] private bool onlyDuringTacticalIntel = true;
    [SerializeField] private string alertSymbol = "?";
    [SerializeField] private string combatSymbol = "!";
    [SerializeField] private Color alertColor = new Color(1f, 0.85f, 0.15f, 1f);
    [SerializeField] private Color combatColor = new Color(1f, 0.15f, 0.1f, 1f);
    [SerializeField] private float pulseSpeed = 5f;
    [SerializeField] private float pulseAmount = 0.12f;

    private Vector3 baseScale = Vector3.one;

    private void Reset()
    {
        enemyAI = GetComponent<EnemyBaseAI>();
        visionSensor = GetComponent<EnemyVisionSensor>();
        anchor = transform;
    }

    private void Awake()
    {
        ResolveReferences();

        if (label == null && autoCreateLabel)
        {
            CreateLabel();
        }

        if (label != null)
        {
            baseScale = label.transform.localScale;
            label.gameObject.SetActive(false);
        }
    }

    private void LateUpdate()
    {
        if (label == null || enemyAI == null)
        {
            return;
        }

        bool reveal = !onlyDuringTacticalIntel ||
                      (visionSensor != null && visionSensor.IsStateIntelVisible);

        string symbol = string.Empty;
        Color color = Color.white;

        if (reveal)
        {
            switch (enemyAI.CurrentState)
            {
                case EnemyState.Alert:
                case EnemyState.Search:
                    symbol = alertSymbol;
                    color = alertColor;
                    break;

                case EnemyState.Combat:
                case EnemyState.Taunt:
                    symbol = combatSymbol;
                    color = combatColor;
                    break;
            }
        }

        bool visible = !string.IsNullOrEmpty(symbol) &&
                       enemyAI.CurrentState != EnemyState.Dead;

        if (label.gameObject.activeSelf != visible)
        {
            label.gameObject.SetActive(visible);
        }

        if (!visible)
        {
            return;
        }

        Transform targetAnchor = anchor != null ? anchor : transform;
        label.transform.position = targetAnchor.position + worldOffset;
        label.transform.rotation = Quaternion.identity;

        label.text = symbol;
        label.color = color;

        float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
        label.transform.localScale = baseScale * pulse;
    }

    private void ResolveReferences()
    {
        if (enemyAI == null)
        {
            enemyAI = GetComponent<EnemyBaseAI>();
        }

        if (visionSensor == null)
        {
            visionSensor = GetComponent<EnemyVisionSensor>();
        }

        if (anchor == null)
        {
            anchor = transform;
        }
    }

    private void CreateLabel()
    {
        GameObject labelObject = new GameObject("AwarenessIndicator");
        labelObject.transform.SetParent(transform, false);
        labelObject.layer = gameObject.layer;

        label = labelObject.AddComponent<TextMeshPro>();
        label.font = TMP_Settings.defaultFontAsset;
        label.fontSize = Mathf.Max(0.1f, fontSize);
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.enableWordWrapping = false;
        label.text = string.Empty;
        Renderer labelRenderer = label.GetComponent<Renderer>();

        if (labelRenderer != null)
        {
            labelRenderer.sortingLayerID = SortingLayer.NameToID(sortingLayerName);
            labelRenderer.sortingOrder = sortingOrder;
        }
    }
}
