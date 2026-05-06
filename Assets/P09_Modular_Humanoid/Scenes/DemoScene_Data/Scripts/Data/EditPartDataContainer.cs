using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace P09.Modular.Humanoid.Data
{
    [CreateAssetMenu(fileName = "EditPartDataContainer",
        menuName = "P09/Modular Humanoid/Create EditPartDataContainer")]
    public class EditPartDataContainer : ScriptableObject
    {
        [SerializeField] private EditPartType _type;

        [Header("Both/Male/Female")] [Range(0, 2)] [SerializeField]
        private int _sexId;

        [SerializeField] private List<ScriptableObject> _partDataList;

        private List<IEditPartData> _cachedPartDataList;
        
        public EditPartType Type => _type;
        public int SexId => _sexId;
        public List<IEditPartData> PartDataList
        {
            get
            {
                if (_cachedPartDataList == null) _cachedPartDataList = _partDataList.Cast<IEditPartData>().ToList();
                return _cachedPartDataList;
            }
        }
    }

    public enum EditPartType
    {
        None = 0,
        Weapon,
        Shield,
        Head,
        Chest,
        Arm,
        Waist,
        Leg,
        Sex,
        HairStyle,
        HairColor,
        Skin,
        EyeColor,
        FacialHair,
        BustSize,
        FaceEmotion,
        FaceType
    }
}