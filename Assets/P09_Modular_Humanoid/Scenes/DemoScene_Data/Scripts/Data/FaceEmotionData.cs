using System;
using UnityEngine;

namespace P09.Modular.Humanoid.Data
{
    [CreateAssetMenu(fileName = "FaceEmotionData", menuName = "P09/Modular Humanoid/Create FaceEmotionData")]
    public sealed class FaceEmotionData : ScriptableObject, IEditPartData
    {
        [SerializeField] private int _contentId;
        [SerializeField] private string _displayName;
        [SerializeField] private string _animationName;
        
        public int ContentId => _contentId;
        public string DisplayName => _displayName;
        public string MeshName => String.Empty;
        public string AnimationName => _animationName;
    }
}