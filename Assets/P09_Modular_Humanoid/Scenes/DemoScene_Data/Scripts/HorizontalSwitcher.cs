using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using P09.Modular.Humanoid.Data;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace P09.Modular.Humanoid
{
    public class HorizontalSwitcher : MonoBehaviour
    {
        [SerializeField] private EditPartType _type;
        [SerializeField] private Text _text;
        [SerializeField] private Image _image;
        [SerializeField] private Button _leftButton;
        [SerializeField] private Button _rightButton;
        
        private int _currentIndex = 0;
        private List<IEditPartData> _dataList = new List<IEditPartData>();
        private UnityAction<EditPartType, int> _onChangeContent;

        public void Init(UnityAction<EditPartType, int> onChangeContent)
        {
            _currentIndex = 0;
            _dataList = DemoPageController.GetAnyEditPartDataList(_type);
            _onChangeContent = onChangeContent;
            if (_dataList == null || _dataList.Count == 0)
            {
                Debug.LogError($"Data list is empty for type {_type}");
                return;
            }
            _leftButton.onClick.RemoveAllListeners();
            _leftButton.onClick.AddListener(OnLeftButtonClick);
            _rightButton.onClick.RemoveAllListeners();
            _rightButton.onClick.AddListener(OnRightButtonClick);
            UpdateView();
        }

        public void Reset()
        {
            _currentIndex = 0;
            UpdateView();
        }

        public void UpdateView()
        {
            if (_dataList == null || _currentIndex >= _dataList.Count)
            {
                Debug.LogWarning($"Invalid state: DataList is null or index {_currentIndex} out of range");
                return;
            }
            if (_text != null)
            {
                _text.text = _dataList[_currentIndex]?.DisplayName;
            }
            if (_image != null)
            {
                var data = _dataList[_currentIndex] as ColorEditPartData;
                _image.sprite = data != null ? data.Icon : null;
            }
        }

        private void OnLeftButtonClick()
        {
            _currentIndex = (_currentIndex - 1 + _dataList.Count) % _dataList.Count;
            _onChangeContent?.Invoke(_type, _dataList[_currentIndex].ContentId);
        }
        
        private void OnRightButtonClick()
        {
            _currentIndex = (_currentIndex + 1) % _dataList.Count;
            _onChangeContent?.Invoke(_type, _dataList[_currentIndex].ContentId);
        }
    }
}
