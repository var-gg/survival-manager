using P09.Modular.Humanoid.Data;
using UnityEngine;

namespace P09.Modular.Humanoid
{
    public sealed class ArmorPage : EditPage
    {
        [SerializeField] private EquipmentFrameView _equipmentFrameView;
        [SerializeField] private EquipmentSelectView _equipmentSelectView;
        public override void Init()
        {
            base.Init();
            _equipmentFrameView.Init(OnClickFrame);
            _equipmentSelectView.Init(_onChangePart);
        }

        public override void UpdateView(bool isReset = false)
        {
            if(!_isActive) return;
            _equipmentFrameView.UpdateView();
            _equipmentSelectView.UpdateView();
        }
        
        private void OnClickFrame(EditPartType editPartType)
        {
            var pageType = editPartType switch
            {
                EditPartType.Weapon or EditPartType.Shield => DemoPageController.PageType.Weapon,
                _ => DemoPageController.PageType.Armor
            };
            
            _onChangePage?.Invoke(pageType);
        }
    }
}