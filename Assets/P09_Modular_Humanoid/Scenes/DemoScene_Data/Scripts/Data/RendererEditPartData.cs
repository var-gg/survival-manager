using UnityEngine;
using UnityEngine.Serialization;

namespace P09.Modular.Humanoid.Data
{
    [CreateAssetMenu(fileName = "RendererEditPartData", menuName = "P09/Modular Humanoid/Create RendererEditPartData")]
    public class RendererEditPartData : ScriptableObject, IEditPartData
    {
        [SerializeField] private int _contentId;
        [SerializeField] private string _displayName;
        [SerializeField] private string _meshName;
        [SerializeField] private Sprite _icon;
        
        public int ContentId => _contentId;
        public string DisplayName => _displayName;
        public string MeshName => _meshName;
        public Sprite Icon => _icon;
    }
}