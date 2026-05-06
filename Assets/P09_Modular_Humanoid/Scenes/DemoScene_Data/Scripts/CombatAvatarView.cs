using System.Collections.Generic;
using System.Linq;
using P09.Modular.Humanoid.Data;
using UnityEngine;

namespace P09.Modular.Humanoid
{
    public class CombatAvatarView : AvatarView
    {
        
        [Header("WeaponGroupData")]
        [SerializeField] private string CombatIdleAnimationName = "P09_Weapon_idle";
        
        public void UpdateWeaponGroup(int sexId,  int weaponGroupId)
        {
            if (weaponGroupId <= 0)
            {
                return;
            }
            var weaponGroupData = DemoPageController.GetWeaponGroupData(weaponGroupId);
            if (weaponGroupData == null)
            {
                Debug.LogWarning($"No weapon motion data found for group ID {weaponGroupId}");
                return;
            }
            
            // WeaponGroupによって、モーションの切り替え
            var animatorController =  sexId == DemoPageController.MaleSexId
                ? _maleAnimatorController
                : _femaleAnimatorController;
            var runtimeOverride = new AnimatorOverrideController(animatorController);
            runtimeOverride[CombatIdleAnimationName] = weaponGroupData.AnimationClip;
            _animator.runtimeAnimatorController = runtimeOverride;
        }
        
    }
}