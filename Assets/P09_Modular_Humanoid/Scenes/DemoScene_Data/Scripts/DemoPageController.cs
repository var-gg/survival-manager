using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using P09.Modular.Humanoid.Data;
using UnityEngine.UI;

namespace P09.Modular.Humanoid
{
    public class DemoPageController : MonoBehaviour
    {
        [Header("View")]
        [SerializeField] private List<EditPage> _editPages;
        [SerializeField] private List<AvatarView> _avatarViews;
        [SerializeField] private TransitionView _transitionView;
        [SerializeField] private Button _resetButton;
        
        [Header("EditPartData")]
        [SerializeField] private List<EditPartDataContainer> _editPartDataList;
        [Header("WeaponGroupData")]
        [SerializeField] private List<WeaponGroupData> _weaponGroupDataList;
        
        private static PageType _currentPage = PageType.Armor;
        
        private static List<EditPartDataContainer> EditPartDataList;
        private static List<WeaponGroupData> WeaponGroupDataList;
        public static AvatarEditData AvatarEditData;
        private List<WeaponEditPartData> _weaponDataList;
        public static readonly int MaleSexId = 1;
        public static readonly int FemaleSexId = 2;
        private static readonly HashSet<EditPartType> BodyPageHidePartTypes = new()
        {
            EditPartType.Head,
            EditPartType.Weapon,
            EditPartType.Shield
        };
        
        public enum PageType
        {
            None = -1,
            Armor,
            Weapon,
            Body
        }
        
        void Start()
        {
            Init();
        }

        private void Init()
        {
            _currentPage = PageType.Armor;
            AvatarEditData = new AvatarEditData();
            EditPartDataList = _editPartDataList;
            WeaponGroupDataList = _weaponGroupDataList;
            _weaponDataList = EditPartDataList.FirstOrDefault(d => d.Type == EditPartType.Weapon).PartDataList.Cast<WeaponEditPartData>()
                .ToList();
            foreach (var editPage in _editPages)
            {
                editPage.SetEventHandlers(OnChangePage, OnChangePart, OnRotateModel, OnChangeFaceEmotion);
                editPage.Init();
            }
            foreach (var avatarView in _avatarViews)
            {
                avatarView.Init();
            }
            _editPages[(int)_currentPage].Show();
            _transitionView.Init(() =>
            {
                OnChangePage(_currentPage == PageType.Body ? PageType.Armor : PageType.Body);
            });
            _resetButton.onClick.RemoveAllListeners();
            _resetButton.onClick.AddListener(() =>
            {
                AvatarEditData = new AvatarEditData();
                UpdateView(isReset: true);
            });
            UpdateView();
        }

        private void UpdateView(bool isReset = false)
        {
            foreach (var avatarView in _avatarViews)
            {
                avatarView.UpdateView();
                if (avatarView as CombatAvatarView)
                {
                    var weaponGroupId = _weaponDataList.FirstOrDefault(d => d.ContentId == AvatarEditData.WeaponId)?.WeaponGroupId ?? 0; 
                    (avatarView as CombatAvatarView).UpdateWeaponGroup(AvatarEditData.SexId, weaponGroupId);
                }
            }
            foreach (var editPage in _editPages)
            {
                editPage.UpdateView(isReset);
            }
        }

        private void OnChangePage(PageType pageType)
        {
            if (_currentPage == pageType)
            {
                return;
            }

            _currentPage = pageType;
            for (int i = 0; i < _editPages.Count; i++)
            {
                if (i == (int)pageType)
                {
                    _editPages[i].Show();
                }
                else
                {
                    _editPages[i].Hide();
                }
            }
            _transitionView.ChangeTransition(pageType == PageType.Body);
            UpdateView();
        }
        
        private void OnChangePart(EditPartType partType, int contentId)
        {
            Debug.Log($"Change part: {partType}");
            var currentId = AvatarEditData.GetCurrentId(partType);
            AvatarEditData.SetId(partType, currentId == contentId ? 0 : contentId);
            if (partType == EditPartType.Weapon)
            {
                // Shied Activate
                var (currentWeaponId, weaponDataList) = GetEditPartData(EditPartType.Weapon);
                var currentWeaponData = weaponDataList.FirstOrDefault(d => d.ContentId == currentWeaponId) as WeaponEditPartData;
                var currentWeaponGroupData = GetWeaponGroupData(currentWeaponData?.WeaponGroupId ?? 0);
                if (currentWeaponData != null && currentWeaponGroupData.IsUnEquippedShield)
                {
                    AvatarEditData.SetId(EditPartType.Shield, 0);
                }
            }
            UpdateView();
        }
        
        private void OnChangeFaceEmotion(int emotionId)
        {
            Debug.Log($"Change face emotion: {emotionId}");
            var dataList = EditPartDataList.FindAll(d => d.Type == EditPartType.FaceEmotion).FirstOrDefault()?.PartDataList;
            if (dataList != null)
            {
                FaceEmotionData currentData = dataList.FirstOrDefault(d => d.ContentId == emotionId) as FaceEmotionData;
                _avatarViews[(int)PageType.Body].ChangeFaceEmotion(currentData?.AnimationName);
            }

            UpdateView();
        }
        
        private void OnRotateModel(bool isLeft, bool isDown)
        {
            Debug.Log($"Rotate model: {isLeft}, {isDown}");
            _avatarViews[(int)_currentPage].RotateModel(isLeft, isDown);
        }
        
        public static (int currentId, List<IEditPartData> dataList) GetEditPartData(EditPartType type)
        {
            var selectId = AvatarEditData.GetCurrentId(type);
            if (_currentPage == PageType.Body && BodyPageHidePartTypes.Contains(type))
            {
                selectId = 0;
            }
            var dataList = EditPartDataList.FindAll(d => d.Type == type && d.SexId == 0).FirstOrDefault()?.PartDataList;
            if (dataList is { Count: > 0 })
            {
                return (selectId, dataList);
            }
            dataList = EditPartDataList.FindAll(d => d.Type == type && d.SexId == AvatarEditData.SexId).FirstOrDefault()?.PartDataList;
            return (selectId, dataList);
        }
        
        public static List<IEditPartData> GetAnyEditPartDataList(EditPartType type)
        {
            return EditPartDataList.FindAll(d => d.Type == type).FirstOrDefault()?.PartDataList;
        }
        
        public static WeaponGroupData GetWeaponGroupData(int weaponGroupId)
        {
            return WeaponGroupDataList.FirstOrDefault(data => data.WeaponGroupId == weaponGroupId);
        }

        private void OnValidate()
        {
            var editPartTypes = Enum.GetValues(typeof(EditPartType)).Cast<EditPartType>().ToList();
            var duplicateTypes = editPartTypes.GroupBy(x => x)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();
            if (duplicateTypes.Count > 0)
            {
                Debug.LogError($"Duplicate EditPartType: {string.Join(", ", duplicateTypes)}");
            }
        }
    }
}