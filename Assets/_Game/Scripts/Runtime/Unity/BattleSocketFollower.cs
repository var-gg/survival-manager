using UnityEngine;

namespace SM.Unity;

public enum BattleSocketFollowMode
{
    PositionOnly = 0,
    YawOnly = 1,
    FullTransform = 2,
}

public sealed class BattleSocketFollower : MonoBehaviour
{
    [SerializeField] private Transform target = null!;
    [SerializeField] private BattleSocketFollowMode followMode = BattleSocketFollowMode.PositionOnly;

    public void Configure(Transform targetTransform, BattleSocketFollowMode mode)
    {
        target = targetTransform;
        followMode = mode;
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        transform.position = target.position;

        switch (followMode)
        {
            case BattleSocketFollowMode.PositionOnly:
                break;

            case BattleSocketFollowMode.YawOnly:
                var euler = target.rotation.eulerAngles;
                transform.rotation = Quaternion.Euler(0f, euler.y, 0f);
                break;

            case BattleSocketFollowMode.FullTransform:
                transform.rotation = target.rotation;
                break;
        }
    }
}
