using UnityEngine;

namespace P09.Modular.Humanoid.Data
{
    [CreateAssetMenu(fileName = "ArmorEditPartData", menuName = "P09/Modular Humanoid/Create ArmorEditPartData")]
    public class ArmorEditPartData : ScriptableObject, IEditPartData
    {
        [SerializeField] private int _equipmentGroupId;
        [SerializeField] private int _contentId;
        [SerializeField] private string _displayName;
        [SerializeField] private string _meshName;
        [SerializeField] private Sprite _femaleIcon;
        [SerializeField] private Sprite _maleIcon;
        
        public int EquipmentGroupId => _equipmentGroupId;
        public int ContentId => _contentId;
        public string DisplayName => _displayName;
        public string MeshName => _meshName;
        public Sprite FemaleIcon => _femaleIcon;
        public Sprite MaleIcon => _maleIcon;
    }
}