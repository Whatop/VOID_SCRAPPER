using System.Collections;
using UnityEngine;

public class PooledEffectAutoRelease : MonoBehaviour
{
    [Header("Auto Release")]
    [SerializeField] private float defaultLifeTime = 0.3f;
    [SerializeField] private bool releaseOnEnable = true;

    private Coroutine releaseRoutine;

    private void OnEnable()
    {
        if (releaseOnEnable)
        {
            Play(defaultLifeTime);
        }
    }

    private void OnDisable()
    {
        if (releaseRoutine != null)
        {
            StopCoroutine(releaseRoutine);
            releaseRoutine = null;
        }
    }

    public void Play(float lifeTime)
    {
        if (releaseRoutine != null)
        {
            StopCoroutine(releaseRoutine);
        }

        releaseRoutine = StartCoroutine(ReleaseRoutine(Mathf.Max(0.01f, lifeTime)));
    }

    private IEnumerator ReleaseRoutine(float lifeTime)
    {
        yield return new WaitForSeconds(lifeTime);

        releaseRoutine = null;

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