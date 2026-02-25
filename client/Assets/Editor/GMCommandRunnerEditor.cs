using UnityEngine;
using UnityEditor;
using ZLockstep.Sync.Command.Commands;

public class GMCommandRunnerEditor : EditorWindow
{
    private string commandInput = "";
    
    [MenuItem("Tools/GM Commands")]
    public static void ShowWindow()
    {
        GetWindow<GMCommandRunnerEditor>("独立GM工具");
    }

    private void OnGUI()
    {
        GUILayout.Label("独立 GM 命令工具", EditorStyles.boldLabel);
        
        EditorGUILayout.Space();
        
        // 主命令输入
        commandInput = EditorGUILayout.TextField("GM指令:", commandInput);
        
        EditorGUILayout.Space();
        
        // 执行按钮
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        
        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
        buttonStyle.fontStyle = FontStyle.Bold;
        buttonStyle.normal.textColor = Color.white;
        
        if (GUILayout.Button("执行命令", buttonStyle, GUILayout.Height(30), GUILayout.Width(120)))
        {
            ExecuteCommand();
        }
        
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space();
        
        // 清空按钮组
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("清空指令", GUILayout.Width(80)))
        {
            commandInput = "";
        }
        
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space();
        
        // 帮助信息
        EditorGUILayout.HelpBox("直接在编辑器中执行GM命令，无需依赖场景中的GMCommandRunner组件", MessageType.Info);
    }

    private void ExecuteCommand()
    {
        if (string.IsNullOrEmpty(commandInput))
        {
            Debug.LogWarning("[GM] 输入为空，未执行任何操作。");
            return;
        }

        Debug.Log($"[GM] 正在执行指令: {commandInput}");

        // 执行具体的命令逻辑
        ProcessCommand(commandInput.ToLower());
    }

    private void ProcessCommand(string cmd)
    {
        switch (cmd)
        {
            case "god":
                Debug.Log("[GM] 上帝模式已切换 (模拟)");
                break;
                
            case "clear":
                Debug.Log("[GM] 清理场景命令执行");
                break;
                
            case "debug":
                Debug.Log("[GM] 调试模式切换");
                break;
                
            default:
                Debug.Log($"[GM] 收到未知指令: {cmd}");
                break;
        }

        // 提交到游戏系统
        SubmitToGameSystem(cmd);
    }

    private void SubmitToGameSystem(string command)
    {
        #if UNITY_EDITOR
        Ra2Demo ra2Demo = Object.FindObjectOfType<Ra2Demo>();
        if (ra2Demo != null)
        {
            var battleGame = ra2Demo.GetBattleGame();
            if (battleGame != null)
            {
                battleGame.SubmitCommand(new GMCommand(command));
                Debug.Log($"[GM] 命令已提交到游戏系统: {command}");
            }
            else
            {
                Debug.LogWarning("[GM] 未找到BattleGame实例");
            }
        }
        else
        {
            Debug.LogWarning("[GM] 未找到Ra2Demo实例，请确保场景中有正确的游戏对象");
        }
        #endif
    }
}