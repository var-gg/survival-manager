using UnityEngine;
using UnityEngine.Serialization;

namespace P09.Modular.Humanoid.Data
{
    [CreateAssetMenu(fileName = "ColorEditPartData", menuName = "P09/Modular Humanoid/Create ColorEditPartData")]
    public class ColorEditPartData : ScriptableObject, IEditPartData
    {
        [SerializeField] private int _contentId;
        [SerializeField] private string _displayName;
        [SerializeField] private string _meshName;
        [SerializeField] private Sprite _icon;
        [SerializeField] private Material _material;
        
        public int ContentId => _contentId;
        public string DisplayName => _displayName;
        public string MeshName => _meshName;
        public Sprite Icon => _icon;
        public Material Material => _material;
    }
}