using P09.Modular.Humanoid.Data;
using UnityEngine;
using UnityEngine.Events;

namespace P09.Modular.Humanoid
{
    public sealed class EditBodyPage : EditPage
    {
        [SerializeField] private HorizontalSwitcher _sexSwitcher;
        [SerializeField] private HorizontalSwitcher _faceTypeSwitcher;
        [SerializeField] private HorizontalSwitcher _hairStyleSwitcher;
        [SerializeField] private HorizontalSwitcher _hairColorSwitcher;
        [SerializeField] private HorizontalSwitcher _skinColorSwitcher;
        [SerializeField] private HorizontalSwitcher _eyeColorSwitcher;
        [SerializeField] private HorizontalSwitcher _facialHairSwitcher;
        [SerializeField] private HorizontalSwitcher _bustSizeSwitcher;
        
        [SerializeField] private HorizontalSwitcher _faceEmotionSwitcher;
        
        public override void Init()
        {
            base.Init();
            _sexSwitcher.Init(_onChangePart);
            _faceTypeSwitcher.Init(_onChangePart);
            _hairStyleSwitcher.Init(_onChangePart);
            _hairColorSwitcher.Init(_onChangePart);
            _skinColorSwitcher.Init(_onChangePart);
            _eyeColorSwitcher.Init(_onChangePart);
            _facialHairSwitcher.Init(_onChangePart);
            _bustSizeSwitcher.Init(_onChangePart);
            
            _faceEmotionSwitcher.Init( (editPartType, contentId) => _onChangeFaceEmotion(contentId) );
        }

        public override void UpdateView(bool isReset = false)
        {
            if(!_isActive) return;
            var switchers = new[]
            {
                _sexSwitcher,
                _faceTypeSwitcher,
                _hairStyleSwitcher,
                _hairColorSwitcher,
                _skinColorSwitcher,
                _eyeColorSwitcher,
                _facialHairSwitcher,
                _bustSizeSwitcher,
                _faceEmotionSwitcher
            };
            foreach (var switcher in switchers)
            {
                switcher.UpdateView();
                if (isReset)
                {
                    switcher.Reset();
                }
            }

            var sexId = DemoPageController.AvatarEditData.SexId;
            _facialHairSwitcher.gameObject.SetActive(sexId == DemoPageController.MaleSexId);
            _bustSizeSwitcher.gameObject.SetActive(sexId == DemoPageController.FemaleSexId);
        }
    }
}