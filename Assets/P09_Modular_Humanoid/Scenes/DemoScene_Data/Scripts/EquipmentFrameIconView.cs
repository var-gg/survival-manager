using System.Collections.Generic;
using System.Linq;
using P09.Modular.Humanoid.Data;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace P09.Modular.Humanoid
{
    public class EquipmentFrameIconView : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private EditPartType _type;
        [SerializeField] private Button _button;
        [SerializeField] private Image _icon;
        [SerializeField] private Image _emptyIcon;

        private List<IEditPartData> _dataList = new();
        private UnityAction<EditPartType> _onClickFrame;

        public void Init(UnityAction<EditPartType> onClickFrame)
        {
            _dataList = DemoPageController.GetEditPartData(_type).dataList;
            _onClickFrame = onClickFrame;
            _icon.sprite = null;
            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(OnClickFrame);
        }

        public void UpdateView()
        {
            var currentEquipmentId = DemoPageController.AvatarEditData.GetCurrentId(_type);
            if (_dataList == null)
            {
                Debug.LogWarning($"Invalid state: DataList is null or index {currentEquipmentId} out of range. Type: {_type}");
                return;
            }

            var currentData = _dataList.FirstOrDefault(d => d.ContentId == currentEquipmentId);
            if (currentData == null || currentEquipmentId == 0)
            {
                _icon.gameObject.SetActive(false);
                _emptyIcon.gameObject.SetActive(true);
            }
            else switch (currentData)
            {
                case RendererEditPartData rendererEditPartData:
                    _icon.gameObject.SetActive(true);
                    _emptyIcon.gameObject.SetActive(false);
                    _icon.sprite = rendererEditPartData.Icon;
                    break;
                case WeaponEditPartData weaponEditPartData:
                    _icon.gameObject.SetActive(true);
                    _emptyIcon.gameObject.SetActive(false);
                    _icon.sprite = weaponEditPartData.Icon;
                    break;
                case ArmorEditPartData armorEditPartData:
                    _icon.gameObject.SetActive(true);
                    _emptyIcon.gameObject.SetActive(false);
                    _icon.sprite = DemoPageController.AvatarEditData.SexId == DemoPageController.MaleSexId
                        ? armorEditPartData.MaleIcon
                        : armorEditPartData.FemaleIcon;
                    break;
            }
        }

        private void OnClickFrame()
        {
            _onClickFrame?.Invoke(_type);
        }
    }
}