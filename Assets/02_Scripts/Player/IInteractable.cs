using UnityEngine;

public interface IInteractable
{
    string InteractionText { get; }
    bool CanInteract(GameObject interactor);
    void Interact(GameObject interactor);
}

/// <summary>
/// E 키를 일정 시간 유지해야 실행되는 월드 상호작용입니다.
/// PlayerInteractor가 유지 시간과 중단 조건을 공통으로 처리합니다.
/// </summary>
public interface IHoldInteractable : IInteractable
{
    float HoldDuration { get; }
    bool CancelHoldOnDamage { get; }

    void OnHoldStarted(GameObject interactor);
    void OnHoldCanceled(GameObject interactor);
}
