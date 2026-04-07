using SM.Combat.Model;
using UnityEngine;

namespace SM.Unity;

public readonly record struct BattleActorVisualState(
    Vector3 LocalPosition,
    Vector3 LocalScale,
    Quaternion LocalRotation,
    Color BodyColor,
    Color ShadowColor);

public abstract class BattleActorVisualAdapter : MonoBehaviour
{
    public abstract Transform? VisualRoot { get; }
    public abstract Renderer? PrimaryRenderer { get; }
    public abstract Renderer? ShadowRenderer { get; }

    public abstract void Initialize(BattleActorWrapper wrapper, BattleUnitReadModel actor);
    public abstract void ApplyState(in BattleActorVisualState state);
}
