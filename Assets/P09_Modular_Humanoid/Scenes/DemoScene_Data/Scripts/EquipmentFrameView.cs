using System.Collections;
using System.Collections.Generic;
using P09.Modular.Humanoid.Data;
using UnityEngine;
using UnityEngine.Events;

namespace P09.Modular.Humanoid
{
    public class EquipmentFrameView : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private EquipmentFrameIconView _weaponFrame;
        [SerializeField] private EquipmentFrameIconView _shieldFrame;
        [SerializeField] private EquipmentFrameIconView _headFrame;
        [SerializeField] private EquipmentFrameIconView _chestFrame;
        [SerializeField] private EquipmentFrameIconView _armFrame;
        [SerializeField] private EquipmentFrameIconView _waistFrame;
        [SerializeField] private EquipmentFrameIconView _legFrame;
        
        private EquipmentFrameIconView[] _frames;
        
        public void Init(UnityAction<EditPartType> onChangePage)
        {
            _root.SetActive(false);
            _frames = new[]
            {
                _weaponFrame, _shieldFrame, _headFrame, _chestFrame, _armFrame, _waistFrame, _legFrame
            };
            foreach (var frame in _frames)
            {
                frame.Init(onChangePage);
            }
        }
        
        public void UpdateView()
        {
            _root.SetActive(true);
            foreach (var frame in _frames)
            {
                frame.UpdateView();
            }
        }
    }
}
