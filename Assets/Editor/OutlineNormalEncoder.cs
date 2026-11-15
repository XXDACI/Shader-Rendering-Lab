using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class OutlineNormalEncoder : EditorWindow
{
    private const float PositionPrecision = 100000.0f;
    private const string EncodedSuffix = "_OutlineEncoded";

    private enum BakeMode
    {
        FullSmooth,
        AngleWeighted
    }

    private GameObject m_TargetObject;
    private Vector2 m_ScrollPosition;
    private Vector2 m_ResultScrollPosition;
    private BakeMode m_BakeMode = BakeMode.AngleWeighted;
    private float m_SmoothAngle = 75.0f;
    private string m_ResultMessage = "请选择一个带 MeshFilter 或 SkinnedMeshRenderer 的物体，然后点击 Encode。";

    [MenuItem("Tools/Outline Encoder")]
    public static void ShowWindow()
    {
        OutlineNormalEncoder window = GetWindow<OutlineNormalEncoder>("Outline Encoder");
        window.minSize = new Vector2(520, 360);
        window.Show();
    }

    private void OnGUI()
    {
        m_ScrollPosition = EditorGUILayout.BeginScrollView(m_ScrollPosition);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Outline Normal Encoder", EditorStyles.boldLabel);
        EditorGUILayout.Space(10);

        EditorGUILayout.HelpBox(
            "这个工具会把描边用的法线提前写进 Mesh：\n\n" +
            "Color.rg = RG 球坐标编码（切线空间）\n" +
            "UV2.xy = Octahedron 八面体编码（切线空间）\n" +
            "UV3.xyz = 完整平滑法线（切线空间）\n\n" +
            "Cube 这类简单硬边模型用 Full Smooth。\n" +
            "人物这类复杂模型用 Angle Weighted，避免过度平均。",
            MessageType.Info
        );

        EditorGUILayout.Space(10);
        DrawTargetObjectSection();

        EditorGUILayout.Space(10);
        DrawBakeSettingsSection();

        EditorGUILayout.Space(10);
        DrawEncodeButtonSection();

        EditorGUILayout.Space(10);
        DrawResultSection();

        EditorGUILayout.EndScrollView();
    }

    private void DrawTargetObjectSection()
    {
        EditorGUILayout.LabelField("Target Object", EditorStyles.boldLabel);

        m_TargetObject = (GameObject)EditorGUILayout.ObjectField(
            "Object",
            m_TargetObject,
            typeof(GameObject),
            true
        );

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Use Selected Object", GUILayout.Height(25)))
        {
            if (Selection.activeGameObject != null)
            {
                m_TargetObject = Selection.activeGameObject;
                m_ResultMessage = "已使用当前选中的物体：" + m_TargetObject.name;
            }
            else
            {
                m_ResultMessage = "当前没有选中任何物体。";
            }
        }

        if (GUILayout.Button("Clear", GUILayout.Height(25)))
        {
            m_TargetObject = null;
            m_ResultMessage = "已清空 Target Object。";
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawBakeSettingsSection()
    {
        EditorGUILayout.LabelField("Bake Settings", EditorStyles.boldLabel);

        m_BakeMode = (BakeMode)EditorGUILayout.EnumPopup("Bake Mode", m_BakeMode);

        using (new EditorGUI.DisabledScope(m_BakeMode != BakeMode.AngleWeighted))
        {
            m_SmoothAngle = EditorGUILayout.Slider("Smooth Angle", m_SmoothAngle, 1.0f, 180.0f);
        }

        if (m_BakeMode == BakeMode.FullSmooth)
        {
            EditorGUILayout.HelpBox("Full Smooth：同位置顶点全部平均。适合 Cube / Box，让硬边描边闭合。", MessageType.None);
        }
        else
        {
            EditorGUILayout.HelpBox("Angle Weighted：按面积和夹角加权，并用 Smooth Angle 限制过度平均。适合人物。", MessageType.None);
        }
    }

    private void DrawEncodeButtonSection()
    {
        EditorGUILayout.LabelField("Encode", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(m_TargetObject == null))
        {
            if (GUILayout.Button("Encode Outline Normals", GUILayout.Height(35)))
            {
                EncodeTargetObject();
            }
        }

        if (m_TargetObject == null)
        {
            EditorGUILayout.HelpBox("请先拖入一个物体，或者点击 Use Selected Object。", MessageType.Warning);
        }
    }

    private void DrawResultSection()
    {
        EditorGUILayout.LabelField("Result", EditorStyles.boldLabel);

        GUIStyle resultStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
        {
            padding = new RectOffset(6, 6, 6, 6)
        };

        EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Height(130), GUILayout.ExpandWidth(true));
        m_ResultScrollPosition = EditorGUILayout.BeginScrollView(m_ResultScrollPosition, false, true);
        EditorGUILayout.LabelField(m_ResultMessage, resultStyle, GUILayout.ExpandWidth(true));
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }
    private void EncodeTargetObject()
    {
        if (m_TargetObject == null)
        {
            m_ResultMessage = "编码失败：Target Object 为空。";
            return;
        }

        int encodedCount = 0;
        List<string> encodedMeshes = new List<string>();

        MeshFilter[] meshFilters = m_TargetObject.GetComponentsInChildren<MeshFilter>(true);

        foreach (MeshFilter meshFilter in meshFilters)
        {
            if (meshFilter.sharedMesh == null)
            {
                continue;
            }

            Mesh oldMesh = meshFilter.sharedMesh;
            Mesh newMesh = CreateEncodedMesh(oldMesh, meshFilter.name, out string path);

            Undo.RecordObject(meshFilter, "Encode Outline Normals");
            meshFilter.sharedMesh = newMesh;
            EditorUtility.SetDirty(meshFilter);
            DestroyPreviousGeneratedMesh(oldMesh, newMesh);

            encodedCount++;
            encodedMeshes.Add(path);
        }

        SkinnedMeshRenderer[] skinnedRenderers = m_TargetObject.GetComponentsInChildren<SkinnedMeshRenderer>(true);

        foreach (SkinnedMeshRenderer skinnedRenderer in skinnedRenderers)
        {
            if (skinnedRenderer.sharedMesh == null)
            {
                continue;
            }

            Mesh oldMesh = skinnedRenderer.sharedMesh;
            Mesh newMesh = CreateEncodedMesh(oldMesh, skinnedRenderer.name, out string path);

            Undo.RecordObject(skinnedRenderer, "Encode Outline Normals");
            skinnedRenderer.sharedMesh = newMesh;
            EditorUtility.SetDirty(skinnedRenderer);
            DestroyPreviousGeneratedMesh(oldMesh, newMesh);

            encodedCount++;
            encodedMeshes.Add(path);
        }

        if (encodedCount == 0)
        {
            m_ResultMessage =
                "编码失败：目标物体下面没有找到 MeshFilter 或 SkinnedMeshRenderer。\n\n" +
                "请确认你拖进去的是 Cube / Sphere / FBX 模型这种带 Mesh 的物体。";

            Debug.LogWarning(m_ResultMessage);
            return;
        }

        m_ResultMessage =
            "编码完成！\n\n" +
            "Target Object: " + m_TargetObject.name + "\n" +
            "Bake Mode: " + m_BakeMode + "\n" +
            "Smooth Angle: " + m_SmoothAngle.ToString("0.0") + "\n" +
            "Encoded Mesh Count: " + encodedCount + "\n\n" +
            "写入内容：\n" +
            "Color.rg = RG 球坐标编码（切线空间）\n" +
            "UV2.xy = Octahedron 八面体编码（切线空间）\n" +
            "UV3.xyz = 完整描边法线（切线空间）\n\n" +
            "编码 Mesh：\n" +
            string.Join("\n", encodedMeshes);

        Debug.Log(m_ResultMessage);
    }

    private Mesh CreateEncodedMesh(Mesh sourceMesh, string objectName, out string assetPath)
    {
        Mesh mesh = Object.Instantiate(sourceMesh);
        mesh.name = GetEncodedMeshName(sourceMesh);

        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;

        EnsureMeshNormalsAndTangents(mesh);

        Vector3[] normals = mesh.normals;
        Vector4[] tangents = mesh.tangents;

        Vector3[] outlineNormals = m_BakeMode == BakeMode.FullSmooth
            ? CalculateFullSmoothNormals(vertices, normals, triangles)
            : CalculateAngleWeightedNormals(vertices, normals, triangles, m_SmoothAngle);

        int vertexCount = mesh.vertexCount;

        Color[] colors = new Color[vertexCount];
        List<Vector2> uv2 = new List<Vector2>(vertexCount);
        List<Vector3> uv3 = new List<Vector3>(vertexCount);

        for (int i = 0; i < vertexCount; i++)
        {
            Vector3 n = ObjectNormalToTangentNormal(outlineNormals[i], normals, tangents, i);

            Vector2 rg = RGEncode(n);
            Vector2 oct = UnitVectorToOctahedron(n);

            colors[i] = new Color(rg.x, rg.y, 0.0f, 1f);
            uv2.Add(oct);
            uv3.Add(n);
        }

        mesh.colors = colors;
        mesh.SetUVs(1, uv2);
        mesh.SetUVs(2, uv3);

        assetPath = mesh.name;
        mesh.hideFlags = HideFlags.DontSaveInBuild;

        return mesh;
    }

    private static void EnsureMeshNormalsAndTangents(Mesh mesh)
    {
        if (mesh.normals == null || mesh.normals.Length != mesh.vertexCount)
        {
            mesh.RecalculateNormals();
        }

        if (mesh.tangents == null || mesh.tangents.Length != mesh.vertexCount)
        {
            mesh.RecalculateTangents();
        }
    }

    private static Vector3 ObjectNormalToTangentNormal(Vector3 objectNormal, Vector3[] normals, Vector4[] tangents, int index)
    {
        Vector3 n = objectNormal.sqrMagnitude > 0.0000001f ? objectNormal.normalized : Vector3.forward;
        Vector3 vertexNormal = index < normals.Length && normals[index].sqrMagnitude > 0.0000001f
            ? normals[index].normalized
            : Vector3.forward;

        Vector4 tangent4 = index < tangents.Length ? tangents[index] : new Vector4(1.0f, 0.0f, 0.0f, 1.0f);
        Vector3 tangent = new Vector3(tangent4.x, tangent4.y, tangent4.z);

        if (tangent.sqrMagnitude < 0.0000001f)
        {
            tangent = Vector3.Cross(Vector3.up, vertexNormal);

            if (tangent.sqrMagnitude < 0.0000001f)
            {
                tangent = Vector3.Cross(Vector3.right, vertexNormal);
            }
        }

        tangent = (tangent - vertexNormal * Vector3.Dot(vertexNormal, tangent));

        if (tangent.sqrMagnitude < 0.0000001f)
        {
            tangent = Vector3.Cross(Vector3.up, vertexNormal);

            if (tangent.sqrMagnitude < 0.0000001f)
            {
                tangent = Vector3.Cross(Vector3.right, vertexNormal);
            }
        }

        tangent.Normalize();

        float tangentSign = tangent4.w < 0.0f ? -1.0f : 1.0f;
        Vector3 bitangent = Vector3.Cross(vertexNormal, tangent) * tangentSign;

        Vector3 tangentNormal = new Vector3(
            Vector3.Dot(n, tangent),
            Vector3.Dot(n, bitangent),
            Vector3.Dot(n, vertexNormal)
        );

        return tangentNormal.sqrMagnitude > 0.0000001f ? tangentNormal.normalized : Vector3.forward;
    }

    private static void DestroyPreviousGeneratedMesh(Mesh oldMesh, Mesh newMesh)
    {
        if (oldMesh == null || oldMesh == newMesh)
        {
            return;
        }

        if (AssetDatabase.Contains(oldMesh))
        {
            return;
        }

        if (!oldMesh.name.EndsWith(EncodedSuffix))
        {
            return;
        }

        Object.DestroyImmediate(oldMesh);
    }
    private static string GetBaseMeshName(Mesh mesh)
    {
        string meshName = mesh.name;

        while (meshName.EndsWith(EncodedSuffix))
        {
            meshName = meshName.Substring(0, meshName.Length - EncodedSuffix.Length);
        }

        return meshName;
    }

    private static string GetEncodedMeshName(Mesh mesh)
    {
        return GetBaseMeshName(mesh) + EncodedSuffix;
    }
    private static Vector3[] CalculateFullSmoothNormals(Vector3[] vertices, Vector3[] normals, int[] triangles)
    {
        Dictionary<Vector3Int, Vector3> normalSums = new Dictionary<Vector3Int, Vector3>();

        for (int i = 0; i < triangles.Length; i += 3)
        {
            int i0 = triangles[i];
            int i1 = triangles[i + 1];
            int i2 = triangles[i + 2];

            Vector3 p0 = vertices[i0];
            Vector3 p1 = vertices[i1];
            Vector3 p2 = vertices[i2];

            Vector3 faceCross = Vector3.Cross(p1 - p0, p2 - p0);
            float areaWeight = faceCross.magnitude;

            if (areaWeight < 0.0000001f)
            {
                continue;
            }

            Vector3 weightedNormal = faceCross.normalized * areaWeight;

            AddNormal(normalSums, p0, weightedNormal);
            AddNormal(normalSums, p1, weightedNormal);
            AddNormal(normalSums, p2, weightedNormal);
        }

        return BuildSmoothNormalArray(vertices, normals, normalSums);
    }

    private static Vector3[] CalculateAngleWeightedNormals(Vector3[] vertices, Vector3[] normals, int[] triangles, float smoothAngle)
    {
        Vector3[] result = new Vector3[vertices.Length];
        float smoothDot = Mathf.Cos(smoothAngle * Mathf.Deg2Rad);
        Dictionary<Vector3Int, List<int>> vertexGroups = BuildVertexGroups(vertices);

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 baseNormal = i < normals.Length && normals[i].sqrMagnitude > 0.0000001f
                ? normals[i].normalized
                : Vector3.forward;

            Vector3 sum = Vector3.zero;
            Vector3Int key = GetPositionKey(vertices[i]);

            if (vertexGroups.TryGetValue(key, out List<int> group))
            {
                foreach (int otherIndex in group)
                {
                    if (otherIndex >= normals.Length || normals[otherIndex].sqrMagnitude < 0.0000001f)
                    {
                        continue;
                    }

                    Vector3 otherNormal = normals[otherIndex].normalized;

                    if (Vector3.Dot(baseNormal, otherNormal) >= smoothDot)
                    {
                        sum += otherNormal;
                    }
                }
            }

            result[i] = sum.sqrMagnitude > 0.0000001f ? sum.normalized : baseNormal;
        }

        AddTriangleWeights(vertices, triangles, result, smoothAngle);

        return result;
    }

    private static void AddTriangleWeights(Vector3[] vertices, int[] triangles, Vector3[] normals, float smoothAngle)
    {
        Dictionary<Vector3Int, Vector3> weightedSums = new Dictionary<Vector3Int, Vector3>();
        float smoothDot = Mathf.Cos(smoothAngle * Mathf.Deg2Rad);

        for (int i = 0; i < triangles.Length; i += 3)
        {
            int i0 = triangles[i];
            int i1 = triangles[i + 1];
            int i2 = triangles[i + 2];

            Vector3 p0 = vertices[i0];
            Vector3 p1 = vertices[i1];
            Vector3 p2 = vertices[i2];

            Vector3 e01 = p1 - p0;
            Vector3 e02 = p2 - p0;
            Vector3 e10 = p0 - p1;
            Vector3 e12 = p2 - p1;
            Vector3 e20 = p0 - p2;
            Vector3 e21 = p1 - p2;

            Vector3 faceCross = Vector3.Cross(e01, e02);
            float areaWeight = faceCross.magnitude;

            if (areaWeight < 0.0000001f)
            {
                continue;
            }

            Vector3 faceNormal = faceCross / areaWeight;

            AddAngleWeightedNormal(weightedSums, p0, normals[i0], faceNormal, areaWeight, Vector3.Angle(e01, e02) * Mathf.Deg2Rad, smoothDot);
            AddAngleWeightedNormal(weightedSums, p1, normals[i1], faceNormal, areaWeight, Vector3.Angle(e12, e10) * Mathf.Deg2Rad, smoothDot);
            AddAngleWeightedNormal(weightedSums, p2, normals[i2], faceNormal, areaWeight, Vector3.Angle(e20, e21) * Mathf.Deg2Rad, smoothDot);
        }

        for (int i = 0; i < normals.Length; i++)
        {
            Vector3Int key = GetPositionKey(vertices[i]);

            if (weightedSums.TryGetValue(key, out Vector3 sum) && sum.sqrMagnitude > 0.0000001f)
            {
                normals[i] = sum.normalized;
            }
        }
    }

    private static void AddAngleWeightedNormal(
        Dictionary<Vector3Int, Vector3> weightedSums,
        Vector3 position,
        Vector3 baseNormal,
        Vector3 faceNormal,
        float areaWeight,
        float angleWeight,
        float smoothDot
    )
    {
        if (Vector3.Dot(baseNormal, faceNormal) < smoothDot)
        {
            return;
        }

        AddNormal(weightedSums, position, faceNormal * areaWeight * angleWeight);
    }

    private static Dictionary<Vector3Int, List<int>> BuildVertexGroups(Vector3[] vertices)
    {
        Dictionary<Vector3Int, List<int>> groups = new Dictionary<Vector3Int, List<int>>();

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3Int key = GetPositionKey(vertices[i]);

            if (!groups.TryGetValue(key, out List<int> list))
            {
                list = new List<int>();
                groups.Add(key, list);
            }

            list.Add(i);
        }

        return groups;
    }

    private static Vector3[] BuildSmoothNormalArray(
        Vector3[] vertices,
        Vector3[] normals,
        Dictionary<Vector3Int, Vector3> normalSums
    )
    {
        Vector3[] smoothNormals = new Vector3[vertices.Length];

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3Int key = GetPositionKey(vertices[i]);

            if (normalSums.TryGetValue(key, out Vector3 n) && n.sqrMagnitude > 0.0000001f)
            {
                smoothNormals[i] = n.normalized;
            }
            else if (i < normals.Length && normals[i].sqrMagnitude > 0.0000001f)
            {
                smoothNormals[i] = normals[i].normalized;
            }
            else
            {
                smoothNormals[i] = Vector3.forward;
            }
        }

        return smoothNormals;
    }

    private static void AddNormal(Dictionary<Vector3Int, Vector3> normalSums, Vector3 position, Vector3 normal)
    {
        Vector3Int key = GetPositionKey(position);

        if (normalSums.ContainsKey(key))
        {
            normalSums[key] += normal;
        }
        else
        {
            normalSums.Add(key, normal);
        }
    }

    private static Vector3Int GetPositionKey(Vector3 position)
    {
        return new Vector3Int(
            Mathf.RoundToInt(position.x * PositionPrecision),
            Mathf.RoundToInt(position.y * PositionPrecision),
            Mathf.RoundToInt(position.z * PositionPrecision)
        );
    }

    private static Vector2 RGEncode(Vector3 n)
    {
        n.Normalize();

        float theta = Mathf.Atan2(n.z, n.x);
        float phi = Mathf.Acos(Mathf.Clamp(n.y, -1.0f, 1.0f));

        float r = theta / (Mathf.PI * 2.0f) + 0.5f;
        float g = phi / Mathf.PI;

        return new Vector2(r, g);
    }

    private static Vector2 UnitVectorToOctahedron(Vector3 n)
    {
        float denominator = Mathf.Abs(n.x) + Mathf.Abs(n.y) + Mathf.Abs(n.z);

        if (denominator < 0.0000001f)
        {
            return new Vector2(0.5f, 0.5f);
        }

        Vector2 p = new Vector2(
            n.x / denominator,
            n.y / denominator
        );

        if (n.z <= 0.0f)
        {
            float oldX = p.x;
            float oldY = p.y;

            p.x = (1.0f - Mathf.Abs(oldY)) * SignNotZero(oldX);
            p.y = (1.0f - Mathf.Abs(oldX)) * SignNotZero(oldY);
        }

        return p * 0.5f + Vector2.one * 0.5f;
    }

    private static float SignNotZero(float value)
    {
        return value >= 0.0f ? 1.0f : -1.0f;
    }


    private static string MakeSafeFileName(string fileName)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(c, '_');
        }

        return fileName;
    }
}
