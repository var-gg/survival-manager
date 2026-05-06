using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

namespace P09.Modular.Humanoid
{
    public sealed class TransitionView : MonoBehaviour
    {
        [SerializeField] Button _button;
        [SerializeField] private Animator _animator;
        private static readonly int IsEditAvatar = Animator.StringToHash("IsEditAvatar");

        public void Init(UnityAction onClickListener)
        {
            ChangeTransition(false);
            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(onClickListener);
        }

        public void ChangeTransition(bool isEditAvatar)
        {
            _animator.SetBool(IsEditAvatar, isEditAvatar);
            StartCoroutine(ClickCooldown());
        }
        
        IEnumerator ClickCooldown()
        {
            _button.interactable = false;
            yield return new WaitForSeconds(1f);
            _button.interactable = true;
        }
    }
}