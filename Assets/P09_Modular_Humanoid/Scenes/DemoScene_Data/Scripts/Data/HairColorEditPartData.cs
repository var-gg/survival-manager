using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

namespace P09.Modular.Humanoid.Data
{
    [CreateAssetMenu(fileName = "HairColorEditPartData", menuName = "P09/Modular Humanoid/Create HairColorEditPartData")]
    public class HairColorEditPartData : ColorEditPartData
    {
        [SerializeField] private HairStyleMaterial[] _hairStyleMaterials;
        [System.Serializable]
        public class HairStyleMaterial
        {
            public int HairStyleId;
            public Material Material;
        }
        
        public Material GetMaterial(int hairStyleId)
        {
            var hairStyleMaterial = _hairStyleMaterials.FirstOrDefault(x => x.HairStyleId == hairStyleId);
            return hairStyleMaterial != null ? hairStyleMaterial.Material : Material;
        }
    }
}