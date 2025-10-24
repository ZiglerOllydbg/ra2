# Demo服务器开发文档


# 思路

## 启动
1. 启动匹配线程
1. 启动房间线程池
1. 启动websocket网络


## 网络
- 使用netty的websocket
- 协议使用json，每个协议都包含type和data字段
- WebSocketSessionManager
    - 使用ConcurrentHashMap管理websocket的channel
    - 线程安全的发送消息函数：使用channel.eventLoop().execute
- websocket的match消息，直接添加匹配线程的消息队列中
- websocket保存房间线程和房间id信息，用于转发其他消息到房间线程。
- 断开连接，如果有房间信息，添加断线任务到房间线程的消息队列中。否则添加断线任务到匹配线程的消息队列中。


## 线程
- 匹配线程MatchThread：仅有一个线程，处理所有匹配请求。
- 房间线程池RoomThread：一个线程多个房间，顺序分配房间到房间线程。
- 网络与线程之间使用消息队列，消息队列使用LinkedBlockingQueue。消息队列结构包含channelId（用于其他线程给客户端发送消息使用），消息类型，消息数据等。
- 线程之间通过任务队列交互：LinkedBlockingQueue<Runnable>
- 线程心跳逻辑（每秒20帧，也是帧同步频率）：
    - 先处理消息队列
    - 再处理任务队列
    - 线程心跳逻辑



## 匹配、创建房间、游戏开始、帧同步协议流程
- 匹配协议：{"type":"match","data":{"name":"xxxx"}}，添加到匹配线程的消息队列中。
- 匹配线程心跳逻辑：处理匹配队列，2个玩家一组，创建房间。
- 将房间分配到房间线程：可以添加房间线程任务队列中。
- 房间线程处理任务，同步绑定websocket的房间线程和房间id。通知客户端准备进入场景，并包含所有玩家信息。{"type":"matchSuccess","data":[{"id":1,"name":"xxxx"},{"id":2,"name":"xxxx"}]}
- 等待所有客户端准备完成ready. {"type":"ready"}
- 全部客户端准备完成，开始游戏。广播：{"type":"gameStart"}
- 客户端收到gameStart开始游戏
- 客户端每帧收集输入数据，通过{"type": "frameInput", "data": {"frame": N+2, inputs: [{"id":1,"input":[1,2,3]},{"id":2,"input":[4,5,6]}]}} 协议,发送给服务器。+2帧，对抗延迟。
- 服务器网络接收frameInput，转发房间线程的消息队列中。
- 房间线程每帧处理消息队列
    1. 将输入保存对应帧数据中。FrameData[frame][playerId] = input
    1. 判断当前帧是否收到所有玩家的输入数据，没有的补充空输入。
    1. 帧同步广播：{"type":"frameSync","data":{"frame":N, "frameData":[{"id":1,"input":[1,2,3]},{"id":2,"input":[4,5,6]}]}}
- 客户端收到frameSync，根据这个协议，进行锁步和追帧处理。













