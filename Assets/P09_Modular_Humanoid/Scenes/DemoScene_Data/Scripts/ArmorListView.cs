using System;
using System.Collections.Generic;
using System.Linq;
using P09.Modular.Humanoid.Data;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace P09.Modular.Humanoid
{
    public sealed class ArmorListView : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private Image[] _headerIcons;
        [SerializeField] private Transform _listRoot;
        [SerializeField] private GridLayoutGroup _gridLayoutGroup;
        [SerializeField] private EquipmentListIconView _equipmentListIconViewPrefab;
        [SerializeField] private RectTransform _gridLayoutGroupRectTransform;
        [SerializeField] private Transform _rowLineRoot;
        [SerializeField] private RectTransform _rowLinePrefab;
        
        private UnityAction<EditPartType, int> _onSelectEquipment;
        private readonly List<EquipmentListIconView> _equipmentListIconViews = new List<EquipmentListIconView>();
        
        public void Init(UnityAction<EditPartType, int> onSelectEquipment)
        {
            _onSelectEquipment = onSelectEquipment;
            _equipmentListIconViews.Clear();
        }

        private void LateUpdate()
        {
            _rowLineRoot.localPosition = _gridLayoutGroupRectTransform.localPosition;
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

            for (var i = 0; i < _headerIcons.Length; i++)
            {
                _headerIcons[i].gameObject.SetActive(i < viewEquipmentList.Count);
                if (i >= viewEquipmentList.Count) continue;
                _headerIcons[i].sprite = viewEquipmentList[i].HeaderIcon;
                _headerIcons[i].SetNativeSize();
            }

            var equipmentDataListDic = viewEquipmentList
                .ToDictionary(selectEquipment => selectEquipment.EditPartType,
                    selectEquipment => DemoPageController.GetEditPartData(selectEquipment.EditPartType).dataList
                        .Cast<ArmorEditPartData>()
                        .Where(d => d.EquipmentGroupId != 0).ToArray());
            
            var maxColumnCount = equipmentDataListDic.Max(d => d.Value.Length);
            for (var i = 0; i < maxColumnCount; i++)
            {
                var equipmentGroupId = i + 1;
                foreach (var (editPartType, equipmentDataList) in equipmentDataListDic)
                {
                    var equipmentData = equipmentDataList.FirstOrDefault(d => d.EquipmentGroupId == equipmentGroupId);
                    var equipmentListIconView = Instantiate(_equipmentListIconViewPrefab, _listRoot);
                    equipmentListIconView.Init(editPartType, equipmentData == null ? null : equipmentData,
                        _onSelectEquipment);
                    _equipmentListIconViews.Add(equipmentListIconView);
                }
            }
            
            DrawRowLine(_equipmentListIconViews.Count);
        }
        
        private void DrawRowLine(int itemCount)
        {
            foreach (Transform child in _rowLineRoot)
            {
                Destroy(child.gameObject);
            }
            
            var rowCount = itemCount / _gridLayoutGroup.constraintCount;

            for (var i = 0; i < rowCount; i++)
            {
                var line = Instantiate(_rowLinePrefab, _rowLineRoot.transform);
                var positionY = (i + 1) * _gridLayoutGroup.cellSize.y;
                positionY += _gridLayoutGroup.spacing.y * i;
                positionY += _gridLayoutGroup.padding.top;
                line.anchoredPosition = new Vector2(0, -positionY);
            }
        }
    }
}
