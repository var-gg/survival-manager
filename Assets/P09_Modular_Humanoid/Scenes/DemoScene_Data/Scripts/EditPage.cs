using UnityEngine;
using UnityEngine.Events;
using P09.Modular.Humanoid.Data;

namespace P09.Modular.Humanoid
{
    public abstract class EditPage : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private ModelRotateButton _modelRotateButton;
        
        protected bool _isActive;
        protected UnityAction<EditPartType, int> _onChangePart;
        protected UnityAction<DemoPageController.PageType> _onChangePage;
        protected UnityAction<int> _onChangeFaceEmotion;

        public virtual void Init()
        {
            Hide();
        }
        
        public void SetEventHandlers(
            UnityAction<DemoPageController.PageType> onChangePage, 
            UnityAction<EditPartType, int> onChangePart,
            UnityAction<bool, bool> onRotateModel,
            UnityAction<int> onChangeFaceEmotion)
        {
            _onChangePage = onChangePage;
            _onChangePart = onChangePart;
            _onChangeFaceEmotion = onChangeFaceEmotion;
            _modelRotateButton.Init(onRotateModel);
        }

        public void Show()
        {
            _isActive = true;
            _root.SetActive(true);
        }
        
        public void Hide()
        {
            _isActive = false;
            _root.SetActive(false);
        }

        public abstract void UpdateView(bool isReset = false);
    }
}