using UnityEngine;

namespace P09.Modular.Humanoid.Data
{
    [CreateAssetMenu(fileName = "WeaponGroupData", menuName = "P09/Modular Humanoid/Create WeaponGroupData")]
    public class WeaponGroupData : ScriptableObject
    {
        [SerializeField] private int _weaponGroupId;
        [Header("盾を外すか")]
        [SerializeField] private bool _isUnEquippedShield;
        [Header("再生するモーション(未設定なら共通モーション)")]
        [SerializeField] private AnimationClip _animationClip;
        
        public int WeaponGroupId => _weaponGroupId;
        public bool IsUnEquippedShield => _isUnEquippedShield;
        public AnimationClip AnimationClip => _animationClip;
    }
}