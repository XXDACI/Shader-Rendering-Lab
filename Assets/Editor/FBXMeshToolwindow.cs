using System;
using System.Text;
using System.Drawing.Printing;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using static UnityEngine.Mesh;
using System.Collections.Generic;

public class FBXMeshToolwindow : EditorWindow
{
    #region Fields

    private Vector2 m_ScrollPosition;
    private Vector2 m_ResultScrollPosition;
    private Vector2 m_OldDataScrollPosition;
    private Vector2 m_NewDataScrollPosition;

    private GameObject m_SourceObject;
    private GameObject m_TargetObject;
    private Material[] materials = new Material[1];
    private Mesh m_SourceMesh;
    private Mesh m_TargetMesh;
    private Dictionary<VertexChannel, string> m_OldDataByChannel = new Dictionary<VertexChannel, string>();

    private bool m_AssignNewMaterial = false;
    private bool assignChanged = false;

    private enum VertexChannel
    {
        Position,
        Normal,
        Tangent,
        Color,
        UV0,
        UV1,
        UV2,
        UV3
    }

    private VertexChannel m_SourceChannel = VertexChannel.Normal;
    private VertexChannel m_TargetChannel = VertexChannel.Color;

    private string m_ResultMessage;
    private string m_OldDataMessage;
    private string m_NewDataMessage;

    private string object_Name;
    private string data_type;

    #endregion


    #region Menu

    [MenuItem("Tools/FBX Mesh Tools")]
    static void ShowWindow()
    {
        var window = GetWindow<FBXMeshToolwindow>("FBX Mesh Tools");
        window.minSize = new Vector2(500, 500);
        window.Show();
    }

    #endregion


    #region GUI

    void OnGUI()
    {
        m_ScrollPosition = EditorGUILayout.BeginScrollView(m_ScrollPosition);

        DrawHeader();

        DrawSourceObjectSection();

        DrawTargetObjectSection();

        DrawOverWriteSection();

        DrawMeshDataDisplayArea();

        DrawResultArea();

        EditorGUILayout.EndScrollView();
    }

    void DrawHeader()
    {
        EditorGUILayout.Space(10);

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize  = 18,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        EditorGUILayout.LabelField("FBX Mesh 工具", titleStyle);

        EditorGUILayout.Space(5);

        GUIStyle descStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true
        };
        EditorGUILayout.LabelField("用于查看模型顶点信息，并在之后支持将一个模型的顶点通道写入另一个模型。", descStyle);

    }

    void DrawSourceObjectSection()
    {
        EditorGUILayout.Space(10);

        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.LabelField("Source Object", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        EditorGUILayout.BeginHorizontal();

        m_SourceObject = (GameObject)EditorGUILayout.ObjectField(
            "FBX 文件:",
            m_SourceObject,
            typeof(GameObject),
            true
        );

        if (GUILayout.Button("浏览", GUILayout.Width(60)))
        {
            string path = EditorUtility.OpenFilePanel(
                "选择 FBX 文件",
                Application.dataPath,
                "fbx"
            );

            m_SourceObject = LoadAssetPath(path);
        }

        EditorGUILayout.EndHorizontal();

        m_SourceMesh = GetMeshFromObject(m_SourceObject);

        if (m_SourceMesh == null)
        {
            EditorGUILayout.HelpBox("无法读取当前Object", MessageType.Warning);
        }
        else
        {
            EditorGUILayout.LabelField("Mesh Name", m_SourceMesh.name);
            EditorGUILayout.LabelField("Vertex Count", m_SourceMesh.vertexCount.ToString());
            EditorGUILayout.LabelField("Triangle Count", (m_SourceMesh.triangles.Length / 3).ToString());

            m_SourceChannel = (VertexChannel)EditorGUILayout.EnumPopup(
                "Source Channel",
                m_SourceChannel
            );

            EditorGUILayout.Space(5);

            // 数据显示
            DataDisplayButtons("Source Object");
        }

        EditorGUILayout.EndVertical();

    }

    void DrawTargetObjectSection()
    {
        EditorGUILayout.Space(10);

        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.LabelField("Target Object", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        EditorGUILayout.BeginHorizontal();

        m_TargetObject = (GameObject)EditorGUILayout.ObjectField(
            "FBX 文件:",
            m_TargetObject,
            typeof(GameObject),
            true
        );

        if (GUILayout.Button("浏览", GUILayout.Width(60)))
        {
            string path = EditorUtility.OpenFilePanel(
                "选择 FBX 文件",
                Application.dataPath,
                "fbx"
            );

            m_TargetObject = LoadAssetPath(path);
        }

        EditorGUILayout.EndHorizontal();

        m_TargetMesh = GetMeshFromObject(m_TargetObject);

        if (m_TargetMesh == null)
        {
            EditorGUILayout.HelpBox("无法读取当前Object", MessageType.Warning);
        }
        else
        {
            EditorGUILayout.LabelField("Mesh Name", m_TargetMesh.name);
            EditorGUILayout.LabelField("Vertex Count", m_TargetMesh.vertexCount.ToString());
            EditorGUILayout.LabelField("Triangle Count", (m_TargetMesh.triangles.Length / 3).ToString());

            m_TargetChannel = (VertexChannel)EditorGUILayout.EnumPopup(
                "Target Channel",
                m_TargetChannel
            );

            // 数据显示
            DataDisplayButtons("Target Object");

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("==========================", EditorStyles.label);
            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Material Menu", EditorStyles.label);

            EditorGUILayout.Space(5);

            m_AssignNewMaterial = EditorGUILayout.Toggle(
                "Assign Material",
                m_AssignNewMaterial
            );

            EditorGUILayout.LabelField("打勾：附加新material         不打勾：附加Source Object 的 material", EditorStyles.miniLabel);

            EditorGUILayout.Space(5);

            EditorGUILayout.LabelField("材质列表", EditorStyles.label);

            if (m_AssignNewMaterial)
            {
                if (assignChanged)
                {
                    materials = new Material[1];
                    assignChanged = false;
                }

                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("+ Add Material"))
                {
                    Array.Resize(ref materials, materials.Length + 1);
                }

                if (GUILayout.Button("- Remove Material") && materials.Length > 0)
                {
                    Array.Resize(ref materials, materials.Length - 1);
                }

                EditorGUILayout.EndHorizontal();

                for (int i = 0; i < materials.Length; i++)
                {
                    materials[i] = (Material)EditorGUILayout.ObjectField(
                        "Material " + i,
                        materials[i],
                        typeof(Material),
                        false
                    );
                }

            }
            else
            {
                if (m_SourceMesh == null)
                {
                    materials = Array.Empty<Material>();
                    EditorGUILayout.LabelField("Materials", "None");
                }
                else
                {
                    Renderer renderer = m_SourceObject.GetComponent<Renderer>();

                    materials = renderer.sharedMaterials;

                    if (materials == null || materials.Length == 0)
                        EditorGUILayout.LabelField("Materials", "None");
                    else
                    {
                        for (int i = 0; i < materials.Length; i++)
                        {
                            Material current_mat = materials[i];

                            EditorGUILayout.LabelField(
                                "Material " + i,
                                current_mat != null ? current_mat.name : "None"
                            );
                        }
                    }
                }

                assignChanged = true;
            }
        }

        EditorGUILayout.EndVertical();
    }

    void DrawOverWriteSection()
    {
        if (m_SourceObject != null && m_TargetObject != null)
        {
            EditorGUILayout.Space(10);

            string displayText = $"写入: {m_SourceChannel} ----> {m_TargetChannel}";

            if (GUILayout.Button(displayText))
            {
                object_Name = "Target Object";
                data_type = m_TargetChannel.ToString();

                // 只保存“当前要被写入覆盖的 Target Channel”的旧数据
                string oldData = BuildChannelDataText(
                    m_TargetMesh,
                    m_TargetChannel
                );

                m_OldDataByChannel[m_TargetChannel] = oldData;

                m_OldDataMessage = oldData;

                MeshOverWrite();

                Repaint();
            }
        }
    }

    void DrawMeshDataDisplayArea()
    {
        EditorGUILayout.Space(10);

        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.LabelField("Data Display", EditorStyles.boldLabel);

        EditorGUILayout.LabelField("Current Object", object_Name);
        EditorGUILayout.LabelField("Data Type", data_type);

        EditorGUILayout.Space(5);

        float totalWidth = EditorGUIUtility.currentViewWidth - 50f;
        float boxWidth = totalWidth / 2f;
        float boxHeight = 320f;

        EditorGUILayout.BeginHorizontal();

        DrawDataTextBox(
            "Old Data",
            ref m_OldDataScrollPosition,
            m_OldDataMessage,
            boxWidth,
            boxHeight
        );

        DrawDataTextBox(
            "New Data",
            ref m_NewDataScrollPosition,
            m_NewDataMessage,
            boxWidth,
            boxHeight
        );

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    void DrawResultArea()
    {
        EditorGUILayout.Space(10);

        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.LabelField("输出信息", EditorStyles.boldLabel);

        m_ResultScrollPosition = EditorGUILayout.BeginScrollView(
            m_ResultScrollPosition,
            GUILayout.MinHeight(160),
            GUILayout.MaxHeight(200)
        );

        GUI.enabled = false;
        EditorGUILayout.TextArea(m_ResultMessage, GUILayout.ExpandHeight(true));
        GUI.enabled = true;

        EditorGUILayout.EndScrollView();

        EditorGUILayout.EndVertical();
    }

    #endregion

    #region HelperMethod

    private GameObject LoadAssetPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return null;

        if (path.StartsWith(Application.dataPath))
        {
            string assetPath = "Assets" + path.Substring(Application.dataPath.Length);
            GameObject loadedObject = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);

            if (loadedObject == null)
            {
                m_ResultMessage = "文件在 Assets 中，但无法加载成 GameObject:\n" + assetPath;
            }
            else
            {
                m_ResultMessage = "成功加载:\n" + assetPath;
            }

            return loadedObject;
        }

        m_ResultMessage = "当前选择的 FBX 不在 Assets 文件夹里:\n" + path;
        return null;
    }

    private Mesh GetMeshFromObject(GameObject object_)
    {
        if (object_ == null)
            return null;

        MeshFilter meshFilter = object_.GetComponent<MeshFilter>();

        if (meshFilter != null)
            return meshFilter.sharedMesh;

        SkinnedMeshRenderer skinnedMeshRenderer = object_.GetComponent<SkinnedMeshRenderer>();

        if (skinnedMeshRenderer != null)
            return skinnedMeshRenderer.sharedMesh;

        return null;
    }

    void DrawDataTextBox(
    string title,
    ref Vector2 scrollPosition,
    string message,
    float width,
    float height)
    {
        EditorGUILayout.BeginVertical(
            "box",
            GUILayout.Width(width),
            GUILayout.Height(height)
        );

        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

        GUIStyle textAreaStyle = new GUIStyle(EditorStyles.textArea)
        {
            wordWrap = false
        };

        scrollPosition = EditorGUILayout.BeginScrollView(
            scrollPosition,
            false,
            false,
            GUILayout.Height(height - 35f)
        );

        GUI.enabled = false;

        EditorGUILayout.TextArea(
            message,
            textAreaStyle,
            GUILayout.ExpandWidth(true),
            GUILayout.ExpandHeight(true)
        );

        GUI.enabled = true;

        EditorGUILayout.EndScrollView();

        EditorGUILayout.EndVertical();
    }

    private void DataDisplayButtons(string obj)
    {
        EditorGUILayout.LabelField("数据查看", EditorStyles.label);

        DrawButtonRow(
            ("Position", VertexChannel.Position),
            ("Normal", VertexChannel.Normal),
            ("Tangent", VertexChannel.Tangent),
            ("Color", VertexChannel.Color),
            obj
        );

        EditorGUILayout.Space(1);

        DrawButtonRow(
            ("UV 0", VertexChannel.UV0),
            ("UV 1", VertexChannel.UV1),
            ("UV 2", VertexChannel.UV2),
            ("UV 3", VertexChannel.UV3),
            obj
        );
    }

    private void DrawButtonRow(
    (string text, VertexChannel channel) button1,
    (string text, VertexChannel channel) button2,
    (string text, VertexChannel channel) button3,
    (string text, VertexChannel channel) button4,
    string obj)
    {
        Rect rowRect = EditorGUILayout.GetControlRect(false, 22f);

        float spacing = 4f;
        float buttonWidth = (rowRect.width - spacing * 3f) / 4f;

        Rect rect1 = new Rect(
            rowRect.x,
            rowRect.y,
            buttonWidth,
            rowRect.height
        );

        Rect rect2 = new Rect(
            rect1.xMax + spacing,
            rowRect.y,
            buttonWidth,
            rowRect.height
        );

        Rect rect3 = new Rect(
            rect2.xMax + spacing,
            rowRect.y,
            buttonWidth,
            rowRect.height
        );

        Rect rect4 = new Rect(
            rect3.xMax + spacing,
            rowRect.y,
            buttonWidth,
            rowRect.height
        );

        if (GUI.Button(rect1, button1.text))
        {
            object_Name = obj;
            data_type = button1.text;

            PrintOutData(obj, button1.channel);
        }

        if (GUI.Button(rect2, button2.text))
        {
            object_Name = obj;
            data_type = button2.text;

            PrintOutData(obj, button2.channel);
        }

        if (GUI.Button(rect3, button3.text))
        {
            object_Name = obj;
            data_type = button3.text;

            PrintOutData(obj, button3.channel);
        }

        if (GUI.Button(rect4, button4.text))
        {
            object_Name = obj;
            data_type = button4.text;

            PrintOutData(obj, button4.channel);
        }
    }

    void PrintOutData(string obj, VertexChannel channel)
    {
        Mesh cur_mesh = obj == "Source Object" ? m_SourceMesh : m_TargetMesh;

        object_Name = obj;
        data_type = channel.ToString();

        if (cur_mesh == null)
        {
            m_NewDataMessage = obj + " 没有 Mesh，无法显示数据。";
            return;
        }

        if (obj == "Source Object")
        {
            // Source Object 不需要 Old Data
            m_OldDataMessage = "当前通道没有旧数据。";
        }
        else
        {
            // Target Object：只有这个 channel 被覆盖过，才显示 Old Data
            if (m_OldDataByChannel.TryGetValue(channel, out string oldData))
            {
                m_OldDataMessage = oldData;
            }
            else
            {
                // 这个通道没有被写入覆盖过，所以 Old Data 为空
                m_OldDataMessage = "当前通道没有旧数据。";
            }
        }


        m_NewDataMessage = BuildChannelDataText(
            cur_mesh,
            channel
        );


        Repaint();
    }

    private string BuildChannelDataText(Mesh cur_mesh, VertexChannel channel)
    {
        if (cur_mesh == null)
        {
            return "Mesh 为空，无法显示数据。";
        }

        switch (channel)
        {
            case VertexChannel.Normal:
                return BuildArrayText(
                    "Normal",
                    cur_mesh.normals,
                    FormatVector3
                );

            case VertexChannel.Tangent:
                return BuildArrayText(
                    "Tangent",
                    cur_mesh.tangents,
                    FormatVector4
                );

            case VertexChannel.Color:
                return BuildArrayText(
                    "Color",
                    cur_mesh.colors,
                    FormatColor
                );

            case VertexChannel.UV0:
                return BuildArrayText(
                    "UV0",
                    cur_mesh.uv,
                    FormatVector2
                );

            case VertexChannel.UV1:
                return BuildArrayText(
                    "UV1",
                    cur_mesh.uv2,
                    FormatVector2
                );

            case VertexChannel.UV2:
                return BuildArrayText(
                    "UV2",
                    cur_mesh.uv3,
                    FormatVector2
                );

            case VertexChannel.UV3:
                return BuildArrayText(
                    "UV3",
                    cur_mesh.uv4,
                    FormatVector2
                );

            default:
                return BuildArrayText(
                    "Position",
                    cur_mesh.vertices,
                    FormatVector3
                );
        }
    }

    private string BuildArrayText<T>(string v, T[] data, Func<T, string> formatter)
    {
        StringBuilder sb = new StringBuilder();

        if (data == null || data.Length == 0)
        {
            sb.AppendLine("Count: 0");
            sb.AppendLine("当前通道没有数据。");
            return sb.ToString();
        }

        sb.AppendLine("Count: " + data.Length);
        sb.AppendLine("------------------------------");

        for (int i = 0; i < data.Length; i++)
        {
            sb.AppendLine(i + ": " + formatter(data[i]));
        }


        return sb.ToString();
    }



    #region Data_Display_Format

    private string FormatVector2(Vector2 v)
    {
        return "(" +
            v.x.ToString("F4") + ", " +
            v.y.ToString("F4") + ")";
    }

    private string FormatVector3(Vector3 v)
    {
        return "(" +
            v.x.ToString("F4") + ", " +
            v.y.ToString("F4") + ", " +
            v.z.ToString("F4") + ")";
    }

    private string FormatVector4(Vector4 v)
    {
        return "(" +
            v.x.ToString("F4") + ", " +
            v.y.ToString("F4") + ", " +
            v.z.ToString("F4") + ", " +
            v.w.ToString("F4") + ")";
    }

    private string FormatColor(Color c)
    {
        return "(" +
            c.r.ToString("F4") + ", " +
            c.g.ToString("F4") + ", " +
            c.b.ToString("F4") + ", " +
            c.a.ToString("F4") + ")";
    }

    #endregion

    /// <summary>
    /// Overwrite Helper Section
    /// </summary>
    #region Load_and_Write_Section

    // 主function: 先加载 再写入
    private void MeshOverWrite()
    {
        if (m_SourceMesh == null || m_TargetMesh == null)
        {
            m_ResultMessage = "Source Mesh 或 Target Mesh 为空。";
            return;
        }

        if (m_SourceMesh.vertexCount != m_TargetMesh.vertexCount)
        {
            m_ResultMessage =
                "写入失败：Source 和 Target 顶点数量不同。\n" +
                "Source Vertex Count: " + m_SourceMesh.vertexCount + "\n" +
                "Target Vertex Count: " + m_TargetMesh.vertexCount;
            return;
        }

        Vector4[] loadVector = OutSourceObjectData();

        if (loadVector == null || loadVector.Length == 0)
        {
            m_ResultMessage = "读取 Source 数据失败。";
            return;
        }

        // 1. 复制 Target Mesh
        Mesh newTargetMesh = Instantiate(m_TargetMesh);
        newTargetMesh.name = m_TargetMesh.name + "_" + m_SourceChannel + "_To_" + m_TargetChannel;

        // 2. 后面的写入函数操作新 Mesh
        m_TargetMesh = newTargetMesh;

        InTargetObjectData(loadVector);

        m_NewDataMessage = BuildChannelDataText(
            m_TargetMesh,
            m_TargetChannel
        );

        // 3. 把新 Mesh 赋给场景中的 Target Object
        AssignMeshToTargetObject(m_TargetObject, newTargetMesh);

        // 4. 给 Target Object 分配材质
        bool materialSuccess = AssignTargetObjectMaterial();

        if (!materialSuccess)
        {
            return;
        }

        // 5. 此时 Target 已经同时拥有新 Mesh 和材质，再保存 Prefab
        string prefabPath =
        SaveTargetAsPrefab(
            m_TargetObject,
            newTargetMesh,
            newTargetMesh.name
        );

        if (string.IsNullOrEmpty(prefabPath))
        {
            m_ResultMessage =
                "Mesh 已经生成，但 Prefab 保存失败。";
                

            return;
        }
        m_ResultMessage =
            "写入完成。\n\n" +
            "Source Channel: " + m_SourceChannel    + "\n" +
            "Target Channel: " + m_TargetChannel    + "\n" +
            "Vertex Count: "   + loadVector.Length  + "\n\n" +
            
            "Prefab Asset:\n"  + prefabPath;

        SceneView.RepaintAll();
    }

    #region Load

    // 读取数据: 根据不同type进行分类讨论
    private Vector4[] OutSourceObjectData()
    {
        Vector4[] loadData;

        switch (m_SourceChannel)
        {
            case VertexChannel.Normal:
                loadData = LoadVector3Data(m_SourceMesh.normals);

                break;

            case VertexChannel.Tangent:
                loadData = LoadVector4Data(m_SourceMesh.tangents);

                break;

            case VertexChannel.Color:
                loadData = LoadColorData(m_SourceMesh.colors);

                break;

            case VertexChannel.UV0:
                loadData = LoadVector2Data(m_SourceMesh.uv);

                break;

            case VertexChannel.UV1:
                loadData = LoadVector2Data(m_SourceMesh.uv2);

                break;

            case VertexChannel.UV2:
                loadData = LoadVector2Data(m_SourceMesh.uv3);

                break;

            case VertexChannel.UV3:
                loadData = LoadVector2Data(m_SourceMesh.uv4);

                break;

            default:
                loadData = LoadVector3Data(m_SourceMesh.vertices);

                break;
        }

        return loadData;
    }

    private Vector4[] LoadVector2Data(Vector2[] dataset)
    {
        Vector4[] loadData = new Vector4[dataset.Length];

        for (int i = 0; i < dataset.Length; i++)
        {
            var data = dataset[i];
            loadData[i] = new Vector4(
                data.x,
                data.y,
                0.0f,
                1.0f
                );
        }
        return loadData;
    }

    private Vector4[] LoadVector3Data(Vector3[] dataset)
    {
        Vector4[] loadData = new Vector4[dataset.Length];

        for (int i = 0; i < dataset.Length; i++)
        {
            var data = dataset[i];
            loadData[i] = new Vector4(
                data.x,
                data.y,
                data.z,
                1.0f
                );
        }
        return loadData;
    }

    private Vector4[] LoadVector4Data(Vector4[] dataset)
    {
        Vector4[] loadData = new Vector4[dataset.Length];

        for (int i = 0; i < dataset.Length; i++)
        {
            var data = dataset[i];
            loadData[i] = new Vector4(
                data.x,
                data.y,
                data.z,
                data.w
                );
        }
        return loadData;
    }

    private Vector4[] LoadColorData(Color[] dataset)
    {
        Vector4[] loadData = new Vector4[dataset.Length];

        for (int i = 0; i < dataset.Length; i++)
        {
            var data = dataset[i];
            loadData[i] = new Vector4(
                data.r,
                data.g,
                data.b,
                data.a
                );
        }
        return loadData;
    }

    #endregion

    #region write

    private bool InTargetObjectData(Vector4[] loadVector)
    {
        if (m_TargetMesh == null)
        {
            m_ResultMessage = "Target Mesh 为空，无法写入。";
            return false;
        }

        if (loadVector == null || loadVector.Length == 0)
        {
            m_ResultMessage = "读取到的数据为空，无法写入。";
            return false;
        }

        if (loadVector.Length != m_TargetMesh.vertexCount)
        {
            m_ResultMessage =
                "写入失败：Source 数据数量和 Target 顶点数量不一致。\n" +
                "Load Data Count: " + loadVector.Length + "\n" +
                "Target Vertex Count: " + m_TargetMesh.vertexCount;

            return false;
        }

        switch (m_TargetChannel)
        {
            case VertexChannel.Position:
                WriteToPosition(loadVector);
                break;

            case VertexChannel.Normal:
                WriteToNormal(loadVector);
                break;

            case VertexChannel.Tangent:
                WriteToTangent(loadVector);
                break;

            case VertexChannel.Color:
                WriteToColor(loadVector);
                break;

            case VertexChannel.UV0:
            case VertexChannel.UV1:
            case VertexChannel.UV2:
            case VertexChannel.UV3:
                WriteToUV(loadVector, m_TargetChannel);
                break;

            default:
                m_ResultMessage = "未知 Target Channel，无法写入。";
                return false;
        }

        m_TargetMesh.RecalculateBounds();
        EditorUtility.SetDirty(m_TargetMesh);

        return true;
    }

    private void WriteToPosition(Vector4[] loadVector)
    {
        Vector3[] positions = new Vector3[m_TargetMesh.vertexCount];

        for (int i = 0; i < m_TargetMesh.vertexCount; i++)
        {
            Vector4 data = loadVector[i];

            positions[i] = new Vector3(
                data.x,
                data.y,
                data.z
            );
        }

        m_TargetMesh.vertices = positions;
        m_TargetMesh.RecalculateBounds();
    }

    private void WriteToNormal(Vector4[] loadVector)
    {
        Vector3[] normals = new Vector3[m_TargetMesh.vertexCount];

        for (int i = 0; i < m_TargetMesh.vertexCount; i++)
        {
            Vector4 data = loadVector[i];

            Vector3 normal;

            if (m_SourceChannel == VertexChannel.Color)
            {
                normal = new Vector3(
                    data.x * 2.0f - 1.0f,
                    data.y * 2.0f - 1.0f,
                    data.z * 2.0f - 1.0f
                );
            }
            else
            {
                normal = new Vector3(
                    data.x,
                    data.y,
                    data.z
                );
            }

            if (normal == Vector3.zero)
            {
                normal = Vector3.up;
            }
            else
            {
                normal = normal.normalized;
            }

            normals[i] = normal;
        }

        m_TargetMesh.normals = normals;
    }

    private void WriteToTangent(Vector4[] loadVector)
    {
        Vector4[] tangents = new Vector4[m_TargetMesh.vertexCount];

        for (int i = 0; i < m_TargetMesh.vertexCount; i++)
        {
            Vector4 data = loadVector[i];

            Vector3 tangentXYZ;

            if (m_SourceChannel == VertexChannel.Color)
            {
                tangentXYZ = new Vector3(
                    data.x * 2.0f - 1.0f,
                    data.y * 2.0f - 1.0f,
                    data.z * 2.0f - 1.0f
                );
            }
            else
            {
                tangentXYZ = new Vector3(
                    data.x,
                    data.y,
                    data.z
                );
            }

            if (tangentXYZ == Vector3.zero)
            {
                tangentXYZ = Vector3.right;
            }
            else
            {
                tangentXYZ = tangentXYZ.normalized;
            }

            float w = 1.0f;

            if (m_SourceChannel == VertexChannel.Tangent)
            {
                w = Mathf.Approximately(data.w, 0.0f) ? 1.0f : data.w;
            }
            else if (m_SourceChannel == VertexChannel.Color)
            {
                w = data.w > 0.5f ? 1.0f : -1.0f;
            }

            tangents[i] = new Vector4(
                tangentXYZ.x,
                tangentXYZ.y,
                tangentXYZ.z,
                w
            );
        }

        m_TargetMesh.tangents = tangents;
    }

    private void WriteToColor(Vector4[] loadVector)
    {
        Color[] colors = new Color[m_TargetMesh.vertexCount];

        Bounds bounds = m_TargetMesh.bounds;

        for (int i = 0; i < m_TargetMesh.vertexCount; i++)
        {
            Vector4 data = loadVector[i];

            if (m_SourceChannel == VertexChannel.Position)
            {
                float r = SafeInverseLerp(bounds.min.x, bounds.max.x, data.x);
                float g = SafeInverseLerp(bounds.min.y, bounds.max.y, data.y);
                float b = SafeInverseLerp(bounds.min.z, bounds.max.z, data.z);

                colors[i] = new Color(
                    r,
                    g,
                    b,
                    1.0f
                );
            }
            else if (m_SourceChannel == VertexChannel.Normal)
            {
                colors[i] = new Color(
                    data.x * 0.5f + 0.5f,
                    data.y * 0.5f + 0.5f,
                    data.z * 0.5f + 0.5f,
                    1.0f
                );
            }
            else if (m_SourceChannel == VertexChannel.Tangent)
            {
                colors[i] = new Color(
                    data.x * 0.5f + 0.5f,
                    data.y * 0.5f + 0.5f,
                    data.z * 0.5f + 0.5f,
                    data.w > 0.0f ? 1.0f : 0.0f
                );
            }
            else if (IsUVChannel(m_SourceChannel))
            {
                colors[i] = new Color(
                    Mathf.Clamp01(data.x),
                    Mathf.Clamp01(data.y),
                    0.0f,
                    1.0f
                );
            }
            else
            {
                colors[i] = new Color(
                    Mathf.Clamp01(data.x),
                    Mathf.Clamp01(data.y),
                    Mathf.Clamp01(data.z),
                    Mathf.Clamp01(data.w)
                );
            }
        }

        m_TargetMesh.colors = colors;
    }

    private void WriteToUV(Vector4[] loadVector, VertexChannel targetChannel)
    {
        Vector2[] uvs = new Vector2[m_TargetMesh.vertexCount];

        Bounds bounds = m_TargetMesh.bounds;

        for (int i = 0; i < m_TargetMesh.vertexCount; i++)
        {
            Vector4 data = loadVector[i];

            if (m_SourceChannel == VertexChannel.Position)
            {
                float u = SafeInverseLerp(bounds.min.x, bounds.max.x, data.x);
                float v = SafeInverseLerp(bounds.min.y, bounds.max.y, data.y);

                uvs[i] = new Vector2(u, v);
            }
            else if (
                m_SourceChannel == VertexChannel.Normal ||
                m_SourceChannel == VertexChannel.Tangent)
            {
                uvs[i] = new Vector2(
                    data.x * 0.5f + 0.5f,
                    data.y * 0.5f + 0.5f
                );
            }
            else if (m_SourceChannel == VertexChannel.Color)
            {
                uvs[i] = new Vector2(
                    Mathf.Clamp01(data.x),
                    Mathf.Clamp01(data.y)
                );
            }
            else
            {
                uvs[i] = new Vector2(
                    data.x,
                    data.y
                );
            }
        }

        switch (targetChannel)
        {
            case VertexChannel.UV0:
                m_TargetMesh.uv = uvs;
                break;

            case VertexChannel.UV1:
                m_TargetMesh.uv2 = uvs;
                break;

            case VertexChannel.UV2:
                m_TargetMesh.uv3 = uvs;
                break;

            case VertexChannel.UV3:
                m_TargetMesh.uv4 = uvs;
                break;
        }
    }

    private bool IsUVChannel(VertexChannel channel)
    {
        return
            channel == VertexChannel.UV0 ||
            channel == VertexChannel.UV1 ||
            channel == VertexChannel.UV2 ||
            channel == VertexChannel.UV3;
    }

    private float SafeInverseLerp(float min, float max, float value)
    {
        if (Mathf.Approximately(min, max))
        {
            return 0.0f;
        }

        return Mathf.InverseLerp(min, max, value);
    }

    private bool AssignTargetObjectMaterial()
    {
        if (materials == null || materials.Length == 0)
        {
            m_ResultMessage = "没有材质加载。";
            return false;
        }

        for (int i = 0; i < materials.Length; i++)
        {
            if (materials[i] == null)
            {
                m_ResultMessage =
                    "Material " + i + " 为空，请先选择材质。";

                return false;
            }
        }

        Renderer targetRenderer = m_TargetObject.GetComponent<Renderer>();

        if (targetRenderer == null)
        {
            m_ResultMessage = "没有找到Target Object的Renderer。";
            return false;
        }

        Undo.RecordObject(targetRenderer, "Assign Materials");

        targetRenderer.sharedMaterials = materials;

        EditorUtility.SetDirty(targetRenderer);

        return true;

    }

    #endregion

    #region save

    private string SaveTargetAsPrefab(
    GameObject targetObject,
    Mesh newMesh,
    string prefabName)
    {
        if (targetObject == null)
        {
            m_ResultMessage = "Target Object 为空，无法保存 Prefab。";
            return null;
        }

        if (newMesh == null)
        {
            m_ResultMessage = "New Mesh 为空，无法保存 Prefab。";
            return null;
        }

        if (EditorUtility.IsPersistent(targetObject))
        {
            m_ResultMessage =
                "Target Object 是 Project 中的资源。\n" +
                "请使用 Hierarchy 里的场景对象生成 Prefab。";

            return null;
        }

        string folderPath = "Assets/GeneratedMeshes";

        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            AssetDatabase.CreateFolder(
                "Assets",
                "GeneratedMeshes"
            );
        }

        string safeName = prefabName.Replace(" ", "_");

        string prefabPath =
            folderPath + "/" + safeName + ".prefab";

        prefabPath =
            AssetDatabase.GenerateUniqueAssetPath(prefabPath);

        /*
         * 第一次先创建 Prefab 文件。
         *
         * 由于 newMesh 目前还是临时对象，
         * 这一次 Prefab 中的 Mesh 引用可能暂时为空。
         * 后面会重新修复。
         */
        PrefabUtility.SaveAsPrefabAsset(
            targetObject,
            prefabPath,
            out bool firstSaveSuccess
        );

        if (!firstSaveSuccess)
        {
            m_ResultMessage = "第一次创建 Prefab 失败。";
            return null;
        }

        /*
         * 把 newMesh 加入 Prefab 文件内部。
         *
         * 这一步不会单独生成 .asset，
         * Mesh 会成为 Prefab 的子资源。
         */
        if (!AssetDatabase.Contains(newMesh))
        {
            AssetDatabase.AddObjectToAsset(
                newMesh,
                prefabPath
            );
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(prefabPath);

        /*
         * 从 Prefab 内部重新找到刚刚嵌入的 Mesh。
         */
        Mesh embeddedMesh = null;

        UnityEngine.Object[] assets =
            AssetDatabase.LoadAllAssetsAtPath(prefabPath);

        foreach (UnityEngine.Object asset in assets)
        {
            if (asset is Mesh mesh &&
                mesh.name == newMesh.name)
            {
                embeddedMesh = mesh;
                break;
            }
        }

        if (embeddedMesh == null)
        {
            m_ResultMessage = "Prefab 已创建，但没有找到嵌入的 Mesh。";

            return null;
        }

        /*
         * 让当前场景里的 Target 也引用持久化后的 Mesh。
         */
        AssignMeshToTargetObject(
            targetObject,
            embeddedMesh
        );

        /*
         * 打开 Prefab 内容，
         * 把嵌入后的 Mesh 重新赋给 Prefab 的 MeshFilter。
         */
        GameObject prefabContents =
            PrefabUtility.LoadPrefabContents(prefabPath);

        try
        {
            AssignMeshToTargetObject(
                prefabContents,
                embeddedMesh
            );

            PrefabUtility.SaveAsPrefabAsset(
                prefabContents,
                prefabPath
            );
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(
                prefabContents
            );
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return prefabPath;
    }

    private void AssignMeshToTargetObject(GameObject targetObject, Mesh newMesh)
    {
        if (targetObject == null || newMesh == null)
            return;

        MeshFilter meshFilter = targetObject.GetComponent<MeshFilter>();

        if (meshFilter != null)
        {
            Undo.RecordObject(meshFilter, "Assign New Mesh");
            meshFilter.sharedMesh = newMesh;
            EditorUtility.SetDirty(meshFilter);
            return;
        }

        SkinnedMeshRenderer skinnedMeshRenderer = targetObject.GetComponent<SkinnedMeshRenderer>();

        if (skinnedMeshRenderer != null)
        {
            Undo.RecordObject(skinnedMeshRenderer, "Assign New Mesh");
            skinnedMeshRenderer.sharedMesh = newMesh;
            EditorUtility.SetDirty(skinnedMeshRenderer);
        }
    }

    #endregion

    #endregion

    #endregion
}
