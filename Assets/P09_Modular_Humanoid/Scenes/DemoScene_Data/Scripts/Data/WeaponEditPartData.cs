using UnityEngine;

namespace P09.Modular.Humanoid.Data
{
    [CreateAssetMenu(fileName = "WeaponEditPartData", menuName = "P09/Modular Humanoid/Create WeaponEditPartData")]
    public class WeaponEditPartData : ScriptableObject, IEditPartData
    {
        [SerializeField] private int _weaponGroupId;
        [SerializeField] private int _contentId;
        [SerializeField] private string _displayName;
        [SerializeField] private string _meshName;
        [SerializeField] private Sprite _icon;
        
        public int WeaponGroupId => _weaponGroupId;
        public int ContentId => _contentId;
        public string DisplayName => _displayName;
        public string MeshName => _meshName;
        public Sprite Icon => _icon;
    }
}