using UnityEngine;
using UnityEngine.InputSystem;

public sealed class NotebookToggleController : MonoBehaviour
{
    [SerializeField] private GameObject notebookRoot;
    [SerializeField] private InputActionReference notebookToggleAction;
    [SerializeField] private GameObject[] blockingModals;
    private InputAction action;
    private bool ownsAction;

    public bool IsNotebookOpen => notebookRoot != null && notebookRoot.activeSelf;

    private void Awake()
    {
        CloseNotebook();
    }

    private void OnEnable()
    {
        action = notebookToggleAction != null ? notebookToggleAction.action : null;
        if (action == null)
        {
            Debug.LogError("[Notebook] NotebookToggleAction is not assigned.", this);
            return;
        }
        action.performed -= OnToggle;
        action.performed += OnToggle;
        ownsAction = !action.enabled;
        if (ownsAction) action.Enable();
    }

    private void OnDisable()
    {
        if (action == null) return;
        action.performed -= OnToggle;
        if (ownsAction) action.Disable();
        ownsAction = false;
        action = null;
    }

    private void Update()
    {
        if (IsNotebookOpen && IsBlocked())
            CloseNotebook();
    }

    private void OnToggle(InputAction.CallbackContext _)
    {
        ToggleNotebook();
    }

    private bool IsBlocked()
    {
        if (blockingModals == null) return false;
        foreach (var modal in blockingModals)
            if (modal != null && modal.activeInHierarchy) return true;
        return false;
    }

    public void OpenNotebook()
    {
        if (notebookRoot != null && !IsBlocked())
            notebookRoot.SetActive(true);
    }

    public void CloseNotebook()
    {
        if (notebookRoot != null)
            notebookRoot.SetActive(false);
    }

    public void ToggleNotebook()
    {
        if (IsNotebookOpen)
            CloseNotebook();
        else
            OpenNotebook();
    }
}
