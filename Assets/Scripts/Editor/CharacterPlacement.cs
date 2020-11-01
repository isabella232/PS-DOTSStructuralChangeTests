using UnityEditor;
using UnityEngine;

public class StructChangeDemoEditorUtil
{
    [MenuItem("StructChangeDemoEditorUtil/PopulateScene")]
    static void PopulateScene()
    {
        var toClone = Selection.activeTransform.gameObject;
        for (int i = -32; i < 32; ++i)
        {
            for (int j = -32; j < 32; ++j)
            {
                var newObject = GameObject.Instantiate(toClone);
                newObject.transform.localPosition = new Vector3(i, 0, j);
            }
        }
    }
}
