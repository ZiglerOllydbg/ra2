using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using NativeWebSocket;
using Newtonsoft.Json.Linq;
using ZLockstep.Sync.Command;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Timers;

namespace Game.RA2.Client
{
    public class WebSocketClient : IDisposable
    {
        private WebSocket webSocket;
        private string clientId;
        private bool isConnected = false;
        private bool matched = false;
        private bool gameStarted = false;
        
        // 心跳检测相关字段
        private Timer pingTimer;
        private DateTime lastPongTime;
        private DateTime lastPingTime; // 记录发送ping的时间
        private long currentPing = -1; // 当前ping值（毫秒），-1表示未计算
        private readonly int pingInterval = 5000; // 5秒发送一次ping
        private readonly int pongTimeout = 10000; // 10秒内没收到pong认为断开
        
        // 移除了消息队列，直接使用事件
        public event Action<string> OnConnected;
        public event Action<string> OnDisconnected;
        public event Action<string> OnError;
        public event Action<MatchSuccessData> OnMatchSuccess;
        public event Action OnGameStart;
        public event Action<FrameSyncData> OnFrameSync;
        public event Action OnPingTimeout; // 新增：Ping超时事件
        public event Action<long> OnPingUpdated; // 新增：Ping值更新事件
        
        public WebSocketClient(string serverUrl, string clientId)
        {
            this.clientId = clientId;
            this.webSocket = new WebSocket(serverUrl);

            // 初始化心跳相关组件
            pingTimer = new Timer(pingInterval);
            SendPing();
            pingTimer.Elapsed += (sender, e) => SendPing();
            lastPongTime = DateTime.UtcNow;
            lastPingTime = DateTime.UtcNow; // 初始化ping时间
            currentPing = -1; // 初始化ping值为未计算状态
            
            // 注册事件处理器
            webSocket.OnOpen += HandleWebSocketOpen;
            webSocket.OnMessage += HandleWebSocketMessage;
            webSocket.OnClose += HandleWebSocketClose;
            webSocket.OnError += HandleWebSocketError;
        }
        
        // 启动心跳检测
        public void StartHeartbeat()
        {
            if (pingTimer != null)
            {
                pingTimer.Start();
                Debug.Log($"[{clientId}] 心跳检测已启动");
            }
        }
        
        // 停止心跳检测
        public void StopHeartbeat()
        {
            if (pingTimer != null)
            {
                pingTimer.Stop();
                Debug.Log($"[{clientId}] 心跳检测已停止");
            }
        }
        
        // 发送ping消息
        public void SendPing()
        {
            if (IsConnected)
            {
                // 记录发送ping的时间
                lastPingTime = DateTime.UtcNow;
                Ping ping = new()
                {
                    type = "ping"
                };

                string json = JsonConvert.SerializeObject(ping);
                SendMessage(json);
                Debug.Log($"[{clientId}] 发送ping");
            }
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
            SendMatchRequest(RoomType.DUO); // 默认为双人房间
        }
        
        public void SendMatchRequest(RoomType roomType)
        {
            var request = new JObject
            {
                ["type"] = "match",
                ["data"] = new JObject
                {
                    ["name"] = clientId,
                    ["roomType"] = roomType.ToString()
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

        public class Ping
        {
            public string type;
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
            string json = Utils.FrameInputProcessor.SerializeFrameInput(frame, command);
            
            SendMessage(json);
            zUDebug.Log($"[{clientId}] 发送第 {frame} 帧输入数据: {json}");
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
            // WebGL平台不需要手动分发消息队列，基于回调机制
#if !UNITY_WEBGL || UNITY_EDITOR
            webSocket?.DispatchMessageQueue();
#endif
            // 检查pong超时
            CheckPongTimeout();
        }
        
        // 检查pong超时
        private void CheckPongTimeout()
        {
            if (isConnected && pingTimer.Enabled)
            {
                var elapsed = DateTime.UtcNow - lastPongTime;
                if (elapsed.TotalMilliseconds > pongTimeout)
                {
                    Debug.Log($"[{clientId}] 心跳超时，连接可能已断开");
                    OnPingTimeout?.Invoke();
                }
            }
        }
        
        // WebSocket事件处理
        private void HandleWebSocketOpen()
        {
            isConnected = true;
            Debug.Log($"[{clientId}] 连接建立成功");
            
            OnConnected?.Invoke("连接建立成功");
            
            // 启动心跳检测
            StartHeartbeat();
            
            // 注意：不再在连接成功后立即发送匹配请求
            // 而是由调用者决定何时发送匹配请求
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
                    case "pong":
                        HandlePong();
                        break;
                    default:
                        Debug.Log($"[{clientId}] 收到未知消息类型: {type}");
                        break;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"处理消息时发生错误: {e.Message}，message：{message}");
            }
        }
        
        // 处理pong消息
        private void HandlePong()
        {
            lastPongTime = DateTime.UtcNow;
            
            // 计算ping值（毫秒）
            currentPing = (long)(lastPongTime - lastPingTime).TotalMilliseconds;
            
            // 触发ping更新事件
            OnPingUpdated?.Invoke(currentPing);
            
            // Debug.Log($"[{clientId}] 收到pong，ping: {currentPing}ms");
        }
        
        private void HandleMatchSuccess(JObject message)
        {
            matched = true;
            
            string roomId = message["roomId"]?.ToString();
            int campId = message["yourCampId"]?.ToObject<int>() ?? 0;
            string token = message["yourToken"]?.ToString();
            var initialState = message["initialState"] as JObject; // 解析初始状态
    
            Debug.Log($"[{clientId}] 匹配成功. 房间ID: {roomId}, CampID: {campId}, Token: {token}. message: {message}");
            
            var matchData = new MatchSuccessData
            {
                RoomId = roomId,
                CampId = campId,
                Token = token,
                InitialState = initialState // 保存初始状态
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
            
            // 停止心跳检测
            StopHeartbeat();
            
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
            // 停止心跳检测
            StopHeartbeat();
            
            // 释放定时器资源
            if (pingTimer != null)
            {
                pingTimer.Dispose();
                pingTimer = null;
            }
            
            webSocket?.Close();
        }
        
        // 获取当前ping值（毫秒），-1表示未计算
        public long CurrentPing => currentPing;
        
        public bool IsConnected => isConnected && webSocket?.State == WebSocketState.Open;
        public bool IsMatched => matched;
        public bool IsGameStarted => gameStarted;
    }
    
    public struct MatchSuccessData
    {
        public string RoomId;
        public int CampId;
        public string Token;
        public JObject InitialState; // 添加初始状态数据
    }
    
    public struct FrameSyncData
    {
        public int Frame;
        public JObject Data;
    }
    
    // 房间类型枚举
    public enum RoomType
    {
        SOLO = 1,   // 1人房间
        DUO = 2,    // 2人房间
        TRIO = 3,   // 3人房间
        QUAD = 4,   // 4人房间
        OCTO = 8    // 8人房间
    }
    
// 阵营颜色枚举已移至独立文件 CampColor.cs 中
}