using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using NativeWebSocket;
using Newtonsoft.Json.Linq;
using ZLockstep.Sync.Command;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Game.RA2.Client
{
    public class WebSocketClient : IDisposable
    {
        private WebSocket webSocket;
        private string clientId;
        private bool isConnected = false;
        private bool matched = false;
        private bool gameStarted = false;
        private System.Random random = new System.Random();
        
        // 移除了消息队列，直接使用事件
        public event Action<string> OnConnected;
        public event Action<string> OnDisconnected;
        public event Action<string> OnError;
        public event Action<MatchSuccessData> OnMatchSuccess;
        public event Action OnGameStart;
        public event Action<FrameSyncData> OnFrameSync;
        
        public WebSocketClient(string serverUrl, string clientId)
        {
            this.clientId = clientId;
            this.webSocket = new WebSocket(serverUrl);
            
            // 注册事件处理器
            webSocket.OnOpen += HandleWebSocketOpen;
            webSocket.OnMessage += HandleWebSocketMessage;
            webSocket.OnClose += HandleWebSocketClose;
            webSocket.OnError += HandleWebSocketError;
        }
        
        public async void Connect()
        {
            if (!isConnected)
            {
                await webSocket.Connect();
            }
        }
        
        public async void Disconnect()
        {
            if (isConnected)
            {
                await webSocket.Close();
            }
        }
        
        public void SendMatchRequest()
        {
            var request = new JObject
            {
                ["type"] = "match",
                ["data"] = new JObject
                {
                    ["name"] = clientId
                }
            };
            
            SendMessage(request.ToString());
            Debug.Log($"[{clientId}] 发送匹配请求: {request}");
        }
        
        public void SendReady()
        {
            var request = new JObject
            {
                ["type"] = "ready"
            };
            
            SendMessage(request.ToString());
            Debug.Log($"[{clientId}] 发送准备就绪");
        }

        public void SendFrameInput(int frame, float moveX = 0, float moveY = 0, float moveZ = 0)
        {
            var request = new JObject
            {
                ["type"] = "frameInput",
                ["frame"] = frame
            };

            var inputs = new JArray();
            var input = new JObject
            {
                ["id"] = clientId
            };

            var inputData = new JArray();
            inputData.Add(moveX);
            inputData.Add(moveY);
            inputData.Add(moveZ);

            input["input"] = inputData;
            inputs.Add(input);

            request["inputs"] = inputs;

            SendMessage(request.ToString());
            Debug.Log($"[{clientId}] 发送第 {frame} 帧输入数据: ({moveX}, {moveY}, {moveZ})");
        }

        public class FrameInput
        {
            public string type;
            public int frame;
            
            public List<MyCommand> data;
        }
        
        public class MyCommand
        {
            public int commandType;
            public ICommand command;
        }
        
        public void SendFrameInput(int frame, ICommand command)
        {
            FrameInput frameInput = new FrameInput
            {
                type = "frameInput",
                frame = frame,
                data = new List<MyCommand>()
            };

            MyCommand myCommand = new MyCommand
            {
                commandType = command.CommandType,
                command = command
            };

            frameInput.data.Add(myCommand);

            string json = JsonConvert.SerializeObject(frameInput);
            
            SendMessage(json);
            Debug.Log($"[{clientId}] 发送第 {frame} 帧输入数据: {json}");
        }

        // 添加一个公共方法，允许外部调用发送帧输入
        public void SendFrameInput(int frame, Vector3 direction)
        {
            SendFrameInput(frame, direction.x, direction.y, direction.z);
        }
        
        private async void SendMessage(string message)
        {
            if (isConnected && webSocket.State == WebSocketState.Open)
            {
                await webSocket.SendText(message);
            }
        }
        
        // 分发WebSocket消息，需要在Update中调用
        public void DispatchMessageQueue()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            webSocket?.DispatchMessageQueue();
#endif
        }
        
        // WebSocket事件处理
        private void HandleWebSocketOpen()
        {
            isConnected = true;
            Debug.Log($"[{clientId}] 连接建立成功");
            
            OnConnected?.Invoke("连接建立成功");
            
            // 连接成功后发送匹配请求
            SendMatchRequest();
        }
        
        private void HandleWebSocketMessage(byte[] data)
        {
            string message = Encoding.UTF8.GetString(data);
            ProcessMessage(message);
        }
        
        private void ProcessMessage(string message)
        {
            try
            {
                var json = JObject.Parse(message);
                string type = json["type"]?.ToString();

                // Debug.Log($"[{clientId}] 收到消息: {message}");
                
                switch (type)
                {
                    case "matchSuccess":
                        HandleMatchSuccess(json);
                        break;
                    case "gameStart":
                        HandleGameStart();
                        break;
                    case "frameSync":
                        HandleFrameSync(json);
                        break;
                    default:
                        Debug.Log($"[{clientId}] 收到未知消息类型: {type}");
                        break;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"处理消息时发生错误: {e.Message}");
            }
        }
        
        private void HandleMatchSuccess(JObject message)
        {
            matched = true;
            
            string roomId = message["roomId"]?.ToString();
            int campId = message["yourCampId"]?.ToObject<int>() ?? 0;
            string token = message["yourToken"]?.ToString();
            
            Debug.Log($"[{clientId}] 匹配成功. 房间ID: {roomId}, CampID: {campId}, Token: {token}");
            
            var matchData = new MatchSuccessData
            {
                RoomId = roomId,
                CampId = campId,
                Token = token
            };
            
            OnMatchSuccess?.Invoke(matchData);
        }
        
        private void HandleGameStart()
        {
            gameStarted = true;
            Debug.Log($"[{clientId}] 游戏开始");
            OnGameStart?.Invoke();
        }
        
        private void HandleFrameSync(JObject message)
        {
            int frame = message["frame"]?.ToObject<int>() ?? 0;
            // Debug.Log($"[{clientId}] 收到帧同步数据 - 帧号: {frame}, 数据: {message}");
            
            var frameData = new FrameSyncData
            {
                Frame = frame,
                Data = message
            };
            
            OnFrameSync?.Invoke(frameData);
            
            // 模拟发送下一帧的输入数据
            // SendFrameInput(frame + 2); // 现在由外部控制何时发送输入
        }
        
        private void HandleWebSocketClose(WebSocketCloseCode closeCode)
        {
            isConnected = false;
            string reason = GetCloseCodeDescription(closeCode);
            Debug.Log($"[{clientId}] 连接关闭: {reason}");
            
            OnDisconnected?.Invoke(reason);
        }
        
        private void HandleWebSocketError(string errorMsg)
        {
            Debug.LogError($"[{clientId}] WebSocket错误: {errorMsg}");
            OnError?.Invoke(errorMsg);
        }
        
        private string GetCloseCodeDescription(WebSocketCloseCode code)
        {
            switch (code)
            {
                case WebSocketCloseCode.Normal: return "正常关闭";
                case WebSocketCloseCode.Abnormal: return "异常关闭";
                case WebSocketCloseCode.ProtocolError: return "协议错误";
                case WebSocketCloseCode.UnsupportedData: return "无效数据";
                case WebSocketCloseCode.PolicyViolation: return "策略违规";
                case WebSocketCloseCode.TooBig: return "消息过大";
                case WebSocketCloseCode.MandatoryExtension: return "需要扩展";
                case WebSocketCloseCode.ServerError: return "服务器错误";
                case WebSocketCloseCode.TlsHandshakeFailure: return "TLS握手失败";
                default: return $"未知代码: {(int)code}";
            }
        }
        
        public void Dispose()
        {
            webSocket?.Close();
        }
        
        public bool IsConnected => isConnected && webSocket?.State == WebSocketState.Open;
        public bool IsMatched => matched;
        public bool IsGameStarted => gameStarted;
    }
    
    public struct MatchSuccessData
    {
        public string RoomId;
        public int CampId;
        public string Token;
    }
    
    public struct FrameSyncData
    {
        public int Frame;
        public JObject Data;
    }
}