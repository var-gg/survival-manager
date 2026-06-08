using UnityEngine;

namespace SM.Unity;

[DisallowMultipleComponent]
internal sealed class BattlePresentationRootMotionRelay : MonoBehaviour
{
    private Animator? _animator;
    private BattleHumanoidAnimationDriver? _driver;

    public void Configure(Animator animator, BattleHumanoidAnimationDriver driver)
    {
        _animator = animator;
        _driver = driver;
    }

    private void OnAnimatorMove()
    {
        var activeAnimator = _animator != null ? _animator : GetComponent<Animator>();
        if (activeAnimator == null)
        {
            return;
        }

        _driver?.ConsumePresentationRootMotion(activeAnimator.deltaPosition);
    }
}
