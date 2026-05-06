using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace P09.Modular.Humanoid
{
    public sealed class ModelRotateButton : MonoBehaviour
    {
        private UnityAction<bool, bool> _onClickRotateListener = null;
        
        public void Init(UnityAction<bool, bool> onClickRotateListener)
        {
            _onClickRotateListener = onClickRotateListener;
        }
        
        public void OnClickDownArrow(bool isLeft)
        {
            _onClickRotateListener?.Invoke(isLeft, true);
        }
        
        public void OnClickUpArrow(bool isLeft)
        {
            _onClickRotateListener?.Invoke(isLeft, false);
        }
    }
}
