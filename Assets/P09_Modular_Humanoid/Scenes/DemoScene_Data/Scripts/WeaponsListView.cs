using System.Collections.Generic;
using System.Linq;
using P09.Modular.Humanoid.Data;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace P09.Modular.Humanoid
{
    public sealed class WeaponsListView : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private Image[] _headerIcons;
        [SerializeField] private Transform _listRoot;
        [SerializeField] private GridLayoutGroup _gridLayoutGroup;
        [SerializeField] private EquipmentListIconView _equipmentListIconViewPrefab;
        
        private UnityAction<EditPartType, int> _onSelectEquipment;
        private readonly List<EquipmentListIconView> _equipmentListIconViews = new List<EquipmentListIconView>();
        
        public void Init(UnityAction<EditPartType, int> onSelectEquipment)
        {
            _onSelectEquipment = onSelectEquipment;
            _equipmentListIconViews.Clear();
        }
        
        public void UpdateView()
        {
            foreach (var equipmentListIconView in _equipmentListIconViews)
            {
                equipmentListIconView.UpdateView();
            }
        }
        
        public void ReCreateList(IReadOnlyList<SelectEquipment> viewEquipmentList)
        {
            _gridLayoutGroup.constraintCount = viewEquipmentList.Count;
            _equipmentListIconViews.Clear();
            foreach (Transform child in _listRoot)
            {
                Destroy(child.gameObject);
            }

            UpdateHeader(viewEquipmentList);

            Dictionary<int, IEditPartData[]> equipmentDataListDic = new();
            for (var i = 0; i < viewEquipmentList.Count; i++)
            {
                var dataList = DemoPageController.GetEditPartData(viewEquipmentList[i].EditPartType).dataList
                    .OfType<WeaponEditPartData>()
                    .Where(d => d.WeaponGroupId == viewEquipmentList[i].SubCategory);
                equipmentDataListDic.Add(viewEquipmentList[i].SubCategory, dataList.Cast<IEditPartData>().ToArray());
            }
            
            int index = 0;
            int maxCount = equipmentDataListDic.Max(d => d.Value.Length) * equipmentDataListDic.Count;
            while (_equipmentListIconViews.Count < maxCount)
            {
                foreach (var (subCategory, equipmentDataList) in equipmentDataListDic)
                {
                    var equipmentListIconView = Instantiate(_equipmentListIconViewPrefab, _listRoot);
                    var selectEquipment = viewEquipmentList.FirstOrDefault(e => e.SubCategory == subCategory);
                    var editPartType = selectEquipment?.EditPartType ?? EditPartType.None;
                    equipmentListIconView.Init(editPartType,
                        index >= equipmentDataList.Length ? null : equipmentDataList[index], _onSelectEquipment);
                    equipmentListIconView.UpdateView();
                    _equipmentListIconViews.Add(equipmentListIconView);
                }
                index++;
            }
        }

        private void UpdateHeader(IReadOnlyList<SelectEquipment> viewEquipmentList)
        {
            for (var i = 0; i < _headerIcons.Length; i++)
            {
                _headerIcons[i].gameObject.SetActive(i < viewEquipmentList.Count);
                if (i >= viewEquipmentList.Count) continue;
                _headerIcons[i].sprite = viewEquipmentList[i].HeaderIcon;
                _headerIcons[i].SetNativeSize();
            }
        }
    }
}
