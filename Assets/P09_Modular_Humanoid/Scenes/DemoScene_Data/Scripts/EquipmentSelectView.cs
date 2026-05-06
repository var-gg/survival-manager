using System.Collections;
using System.Collections.Generic;
using System.Linq;
using P09.Modular.Humanoid.Data;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace P09.Modular.Humanoid
{
    [System.Serializable]
    public class SelectEquipment
    {
        public EditPartType EditPartType;
        public int SubCategory = 0;
        public Sprite HeaderIcon;
    }
    public sealed class EquipmentSelectView : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private SelectEquipment[] _selectEquipments;
        [SerializeField] private WeaponsListView _weaponsListView = null;
        [SerializeField] private ArmorListView _armorListView = null;
        [SerializeField] private Button _leftButton;
        [SerializeField] private Button _rightButton;
        
        private int _currentIndex = 0;
        private List<SelectEquipment> _viewEquipmentList;
        private const int MaxViewCount = 5;
        
        public void Init(UnityAction<EditPartType, int> onSelectEquipment)
        {
            _root.SetActive(false);
            _viewEquipmentList = _selectEquipments.Take(MaxViewCount).ToList();
            if (_weaponsListView != null) _weaponsListView.Init(onSelectEquipment);
            if (_armorListView != null) _armorListView.Init(onSelectEquipment);
            _leftButton.onClick.RemoveAllListeners();
            _leftButton.onClick.AddListener(OnClickLeftButton);
            _rightButton.onClick.RemoveAllListeners();
            _rightButton.onClick.AddListener(OnClickRightButton);
            
            UpdateView(isChangeCategory: true);
        }
        
        public void UpdateView(bool isChangeCategory = false)
        {
            _root.SetActive(true);
            if (isChangeCategory)
            {
                if (_weaponsListView != null) _weaponsListView.ReCreateList(_viewEquipmentList);
                if (_armorListView != null) _armorListView.ReCreateList(_viewEquipmentList);
            }
            else
            {
                if (_weaponsListView != null) _weaponsListView.UpdateView();
                if (_armorListView != null) _armorListView.UpdateView();
            }
            _leftButton.gameObject.SetActive(_currentIndex > 0);
            _rightButton.gameObject.SetActive(_currentIndex < _selectEquipments.Length - MaxViewCount);
        }

        private void OnClickLeftButton()
        {
            _currentIndex--;
            if (_currentIndex < 0) _currentIndex = 0;
            _viewEquipmentList = _selectEquipments.Skip(_currentIndex).Take(MaxViewCount).ToList();
            UpdateView(isChangeCategory: true);
        }

        private void OnClickRightButton()
        {
            _currentIndex++;
            if (_currentIndex >= _selectEquipments.Length - MaxViewCount) _currentIndex = _selectEquipments.Length - MaxViewCount;
            _viewEquipmentList = _selectEquipments.Skip(_currentIndex).Take(MaxViewCount).ToList();
            UpdateView(isChangeCategory: true);
        }
    }
}
