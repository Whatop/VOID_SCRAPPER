using UnityEngine.InputSystem;

public static class InputBindingUtility
{
    public static InputAction ResolveAction(
        InputActionAsset inputActions,
        string actionMapName,
        string actionName)
    {
        if (inputActions == null || string.IsNullOrWhiteSpace(actionName))
        {
            return null;
        }

        InputBindingPersistence.LoadOnce(inputActions);

        if (!string.IsNullOrWhiteSpace(actionMapName))
        {
            InputActionMap map = inputActions.FindActionMap(actionMapName, false);
            InputAction mappedAction = map != null ? map.FindAction(actionName, false) : null;

            if (mappedAction != null)
            {
                return mappedAction;
            }
        }

        return inputActions.FindAction(actionName, false);
    }

    public static string GetDisplayString(
        InputActionAsset inputActions,
        string actionMapName,
        string actionName,
        string fallback,
        string bindingGroup = null,
        int preferredBindingIndex = -1)
    {
        InputAction action = ResolveAction(inputActions, actionMapName, actionName);

        if (action == null)
        {
            return fallback;
        }

        int bindingIndex = ResolveBindingIndex(action, bindingGroup, preferredBindingIndex);
        if (bindingIndex < 0)
        {
            return fallback;
        }

        string display = action.GetBindingDisplayString(
            bindingIndex,
            out _,
            out _,
            InputBinding.DisplayStringOptions.DontIncludeInteractions
        );

        return string.IsNullOrWhiteSpace(display) ? fallback : display;
    }

    public static int ResolveBindingIndex(
        InputAction action,
        string bindingGroup = null,
        int preferredBindingIndex = -1)
    {
        if (action == null)
        {
            return -1;
        }

        if (preferredBindingIndex >= 0 && preferredBindingIndex < action.bindings.Count)
        {
            return preferredBindingIndex;
        }

        for (int i = 0; i < action.bindings.Count; i++)
        {
            InputBinding binding = action.bindings[i];

            if (binding.isComposite)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(bindingGroup) &&
                (string.IsNullOrWhiteSpace(binding.groups) ||
                 !binding.groups.Contains(bindingGroup)))
            {
                continue;
            }

            return i;
        }

        return -1;
    }
}
