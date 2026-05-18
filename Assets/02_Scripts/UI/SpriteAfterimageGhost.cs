using UnityEngine;

public class SpriteAfterimageGhost : MonoBehaviour
{
    private SpriteRenderer[] renderers;
    private Color[] startColors;

    private float lifeTime = 0.2f;
    private float timer;

    public void Initialize(float duration)
    {
        lifeTime = Mathf.Max(0.01f, duration);
        timer = 0f;

        renderers = GetComponentsInChildren<SpriteRenderer>(true);
        startColors = new Color[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            startColors[i] = renderers[i].color;
        }
    }

    private void Update()
    {
        timer += Time.deltaTime;

        float t = Mathf.Clamp01(timer / lifeTime);
        float alphaMultiplier = 1f - t;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
            {
                continue;
            }

            Color color = startColors[i];
            color.a *= alphaMultiplier;
            renderers[i].color = color;
        }

        if (timer >= lifeTime)
        {
            Destroy(gameObject);
        }
    }
}