using UnityEngine;

// 기존 Player 프리팹에서 Missing Script가 발생하지 않도록 남겨 둔 호환용 컴포넌트입니다.
// 플레이어 비주얼은 PlayerShipVisualController가 SpriteRenderer를 직접 교체합니다.
[DisallowMultipleComponent]
[AddComponentMenu("")]
public class PlayerAnimationController : MonoBehaviour
{
    [SerializeField] private bool disableOnAwake = true;

    private void Awake()
    {
        if (disableOnAwake)
        {
            enabled = false;
        }
    }
}
