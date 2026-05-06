using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using P09.Modular.Humanoid.Data;

namespace P09.Modular.Humanoid
{
    public class AvatarView : MonoBehaviour
    {
        [SerializeField] private Transform _modelRoot;
        [SerializeField] protected Animator _animator;
        [SerializeField] protected RuntimeAnimatorController _maleAnimatorController;
        [SerializeField] protected RuntimeAnimatorController _femaleAnimatorController;
        
        private DemoPageController.PageType _pageType;
        private bool _isRotating = false;
        private bool _isLeft = false;
        private int _currentSexId;

        private const float RotateSpeed = 100f;
        private const string SkinMaterialPattern = @"^P09_.*_Skin.*$";
        private const string EyeMaterialPattern = @"^P09_Eye.*$";

        private static readonly EditPartType[] EquipmentTypes = new[]
        {
            EditPartType.Weapon,
            EditPartType.Shield,
            EditPartType.Head,
            EditPartType.Chest,
            EditPartType.Arm,
            EditPartType.Waist,
            EditPartType.Leg
        };

        public void Init()
        {
            _isRotating = false;
            _isLeft = false;
            _modelRoot.localEulerAngles = Vector3.zero;
        }
        
        private void Update()
        {
            if (!_isRotating)
            {
                return;
            }

            var rotateSpeed = RotateSpeed * Time.deltaTime;
            var rotateY = _isLeft ? rotateSpeed : -rotateSpeed;
            _modelRoot.Rotate(0, rotateY, 0);
        }

        public void ChangeFaceEmotion(string faceEmotionName)
        {
            _animator.Play(faceEmotionName);
        }
        
        public void RotateModel(bool isLeft, bool isRotate)
        {
            _isLeft = isLeft;
            _isRotating = isRotate;
        }

        public void UpdateView()
        {
            _currentSexId = DemoPageController.AvatarEditData.SexId;

            _animator.runtimeAnimatorController = _currentSexId == DemoPageController.MaleSexId
                ? _maleAnimatorController
                : _femaleAnimatorController;
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                // Sex
                var (currentSexId, sexDataList) = DemoPageController.GetEditPartData(EditPartType.Sex);
                UpdateRenderer(child, currentSexId, sexDataList);
                // FaceType
                var (currentFaceTypeId, faceTypeDataList) = DemoPageController.GetEditPartData(EditPartType.FaceType);
                UpdateRenderer(child, currentFaceTypeId, faceTypeDataList);
                // HairStyle
                var (currentHairStyleId, hairStyleDataList) = DemoPageController.GetEditPartData(EditPartType.HairStyle);
                UpdateRenderer(child, currentHairStyleId, hairStyleDataList);
                // HairColor
                var (currentHairColorId, hairColorDataList) = DemoPageController.GetEditPartData(EditPartType.HairColor);
                UpdateHairColor(child, currentHairColorId, hairColorDataList);
                // SkinColor
                var (currentSkinColorId, skinColorDataList) = DemoPageController.GetEditPartData(EditPartType.Skin);
                UpdateSkinColor(child, currentSkinColorId, skinColorDataList);
                // EyeColor
                var (currentEyeColorId, eyeColorDataList) = DemoPageController.GetEditPartData(EditPartType.EyeColor);
                UpdateEyeColor(child, currentEyeColorId, eyeColorDataList);
                if (_currentSexId == DemoPageController.MaleSexId)
                {
                    // FacialHair
                    var (currentFacialHairId, facialHairDataList) = DemoPageController.GetEditPartData(EditPartType.FacialHair);
                    UpdateRenderer(child, currentFacialHairId, facialHairDataList);
                }
                else if (_currentSexId == DemoPageController.FemaleSexId)
                {
                    // BustSize
                    var (currentBustSizeId, bustSizeDataList) = DemoPageController.GetEditPartData(EditPartType.BustSize);
                    UpdateBustSize(child, currentBustSizeId, bustSizeDataList);
                }
                
                // Equipment
                foreach (var equipmentType in EquipmentTypes)
                {
                    var (currentEquipmentId, equipmentDataList) = DemoPageController.GetEditPartData(equipmentType);
                    UpdateRenderer(child, currentEquipmentId, equipmentDataList);
                }
            }
        }
        
        private void UpdateRenderer(Transform child, int currentId, List<IEditPartData> dataList)
        {
            foreach (var data in dataList)
            {
                if (child.name == data.MeshName)
                {
                    child.gameObject.SetActive(data.ContentId == currentId);
                }
                else if(child.name == string.Format(data.MeshName, "Male"))
                {
                    child.gameObject.SetActive(_currentSexId == DemoPageController.MaleSexId && data.ContentId == currentId);
                }
                else if (child.name == string.Format(data.MeshName, "Female") || child.name == string.Format(data.MeshName, "Fem"))
                {
                    child.gameObject.SetActive(_currentSexId == DemoPageController.FemaleSexId && data.ContentId == currentId);
                }
            }
        }

        private void UpdateColor(Transform child, int currentId, List<IEditPartData> dataList)
        {
            var currentData = dataList.FirstOrDefault(d => d.ContentId == currentId); 
            foreach (var data in dataList)
            {
                if (child.name == string.Format(data.MeshName, data.DisplayName))
                {
                    var renderer = child.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        renderer.material = (currentData as ColorEditPartData)?.Material;
                    }
                }
            }
        }
        
        private void UpdateHairColor(Transform child, int currentId, List<IEditPartData> dataList)
        {
            var currentData = dataList.FirstOrDefault(d => d.ContentId == currentId); 
            var hairStyleId = DemoPageController.AvatarEditData.HairStyleId;
            foreach (var data in dataList)
            {
                if (child.name == string.Format(data.MeshName, hairStyleId))
                {
                    var renderer = child.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        renderer.material = (currentData as HairColorEditPartData)?.GetMaterial(hairStyleId);
                    }
                }
            }
        }
        
        private void UpdateSkinColor(Transform child, int currentId, List<IEditPartData> dataList)
        {
            var renderer = child.GetComponent<Renderer>();
            if (renderer == null) return;
            Material[] materials = renderer.materials;
            for (int i = 0; i < materials.Length; i++)
            {
                var material = materials[i];
                if (Regex.IsMatch(material.name, SkinMaterialPattern))
                {
                    var currentData = dataList.FirstOrDefault(d => d.ContentId == currentId);
                    materials[i] = (currentData as ColorEditPartData)?.Material;
                }
            }
            renderer.materials = materials;
        }

        private void UpdateEyeColor(Transform child, int currentId, List<IEditPartData> dataList)
        {
            var currentData = dataList.FirstOrDefault(d => d.ContentId == currentId);
            if (currentData != null && !child.name.Contains(currentData.MeshName)) return;
            var renderers = child.GetComponentsInChildren<Renderer>(includeInactive: true);
            if (renderers == null) return;
            foreach (var renderer in renderers)
            {
                Material[] materials = renderer.materials;
                for (int i = 0; i < materials.Length; i++)
                {
                    var material = materials[i];
                    if (Regex.IsMatch(material.name, EyeMaterialPattern))
                    {
                        materials[i] = (currentData as ColorEditPartData)?.Material;
                    }
                }
                renderer.materials = materials;
            }
        }

        private void UpdateBustSize(Transform child, int currentId, List<IEditPartData> dataList)
        {
            var currentData = dataList.FirstOrDefault(d => d.ContentId == currentId) as BustSizeEditPartData;
            if (currentData == null) return;
            if (child.name != string.Format(currentData.MeshName, "R") &&
                child.name != string.Format(currentData.MeshName, "L")) return;
            child.localScale = currentData.Size;
        }
    }
}
