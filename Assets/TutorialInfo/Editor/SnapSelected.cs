using UnityEngine;
using UnityEditor;

public class SnapSelected : MonoBehaviour
{
    // Snaps selected objects and all their children to clean 0.5 or 1.0 increments
    [MenuItem("Tools/Snap Entire Build & Children %#w")] // Ctrl + Shift + W
    public static void SnapHierarchy()
    {
        Transform[] selected = Selection.transforms;
        if (selected.Length == 0) return;

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Snap Entire Build & Children");

        foreach (Transform root in selected)
        {
            // Snap the parent first
            Undo.RecordObject(root, "Snap Transform");
            root.position = RoundVector(root.position);

            // Snap every child object inside
            Transform[] allChildren = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform child in allChildren)
            {
                Undo.RecordObject(child, "Snap Transform");
                // Snapping localPosition keeps the internal offsets neat and symmetrical
                child.localPosition = RoundVector(child.localPosition);
            }
        }

        Undo.CollapseUndoOperations(undoGroup);
    }

    private static Vector3 RoundVector(Vector3 v)
    {
        // Change to 0.5f if you build on half-meter increments
        float step = 1.0f; 
        return new Vector3(
            Mathf.Round(v.x / step) * step,
            Mathf.Round(v.y / step) * step,
            Mathf.Round(v.z / step) * step
        );
    }
}