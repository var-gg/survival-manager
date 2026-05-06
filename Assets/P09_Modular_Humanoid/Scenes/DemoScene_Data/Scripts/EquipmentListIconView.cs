using P09.Modular.Humanoid.Data;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace P09.Modular.Humanoid
{
    public class EquipmentListIconView : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private Button _button;
        [SerializeField] private GameObject _equipmentRoot;
        [SerializeField] private Image _equipmentIcon;
        [SerializeField] private GameObject _equipedIcon;
        
        private EditPartType _editPartType;
        private IEditPartData _partData;
        private UnityAction<EditPartType, int> _onSelectedEvent = null;

        public void Init(EditPartType editPartType, IEditPartData partData, UnityAction<EditPartType, int> onSelectedEvent)
        {
            _editPartType = editPartType;
            _partData = partData;
            _onSelectedEvent = onSelectedEvent;
            _root.SetActive(false);
            _button.enabled = partData != null;
            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(OnClickButton);
        }
        
        public void UpdateView()
        {
            _root.SetActive(true);
            var currentId = DemoPageController.AvatarEditData.GetCurrentId(_editPartType);
            switch (_partData)
            {
                case WeaponEditPartData weaponEditPartData:
                    _equipmentIcon.sprite = weaponEditPartData.Icon;
                    break;
                case ArmorEditPartData armorEditPartData:
                    _equipmentIcon.sprite = DemoPageController.AvatarEditData.SexId == DemoPageController.MaleSexId
                        ? armorEditPartData.MaleIcon
                        : armorEditPartData.FemaleIcon;
                    break;
                case RendererEditPartData rendererEditPartData:
                    _equipmentIcon.sprite = rendererEditPartData.Icon;
                    break;
                default:
                    _equipmentRoot.SetActive(false);
                    break;
            }
            _equipedIcon.SetActive(_partData?.ContentId == currentId);
        }

        private void OnClickButton()
        {
            _onSelectedEvent?.Invoke(_editPartType, _partData?.ContentId ?? 0);
        }
    }
}
