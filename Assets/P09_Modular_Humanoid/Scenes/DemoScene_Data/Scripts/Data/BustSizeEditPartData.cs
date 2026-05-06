using UnityEngine;
using UnityEngine.Serialization;

namespace P09.Modular.Humanoid.Data
{
    [CreateAssetMenu(fileName = "BustSizeEditPartData", menuName = "P09/Modular Humanoid/Create BustSizeEditPartData")]
    public class BustSizeEditPartData : ScriptableObject, IEditPartData
    {
        [SerializeField] private int _contentId;
        [SerializeField] private string _displayName;
        [SerializeField] private string _meshName;
        [SerializeField] private Vector3 _size = Vector3.one;
        
        public int ContentId => _contentId;
        public string DisplayName => _displayName;
        public string MeshName => _meshName;
        public Vector3 Size => _size;
    }
}