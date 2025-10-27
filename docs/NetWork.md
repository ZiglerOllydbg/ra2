# 网络通信协议文档

本文档描述了客户端与服务器之间的网络通信协议，包括各种消息类型、数据格式和交互流程。

## 消息格式

所有消息都使用JSON格式进行传输，基本结构如下：

```json
{
  "type": "消息类型",
  "data": {
    ..
  }
}
```

## 客户端发送的消息类型

### 1. match - 匹配请求

客户端连接成功后发送匹配请求，加入匹配队列。

**消息格式：**
```json
{
  "type": "match",
  "data": {
    "name": "玩家名称"
  }
}
```



### 2. ready - 准备就绪

玩家在匹配成功后发送准备信号，表示已准备好开始游戏。

**消息格式：**
```json
{
  "type": "ready"
}
```



### 3. frameInput - 帧输入数据

玩家发送游戏输入数据，用于帧同步。

**消息格式：**
```json
{
  "type": "frameInput",
  "frame": 10,
  "inputs": [
    {
      "id": "玩家ID",
      "input": [1, 2, 3]
    }
  ]
}
```



### 4. leave - 离开房间

玩家主动离开房间。

**消息格式：**
```json
{
  "type": "leave"
}
```



## 服务器发送的消息类型

### 1. matched - 匹配中

服务器通知客户端已加入匹配队列。

**消息格式：**
```json
{
  "type": "matched"
}
```



### 2. matchSuccess - 匹配成功

匹配成功，服务器发送房间信息和玩家阵营。

**消息格式：**
```json
{
  "type": "matchSuccess",
  "roomId": "房间ID",
  "yourCampId": 1,
  "yourToken": "玩家token",
  "data": [
    {
      "campId": 1,
      "name": "玩家1名称"
    },
    {
      "campId": 2,
      "name": "玩家2名称"
    }
  ]
}
```

- yourCampId：表示当前客户端的阵营ID
- yourToken：为后续断线重连预留的令牌

### 3. gameStart - 游戏开始

所有玩家准备就绪后，服务器通知游戏开始。

**消息格式：**
```json
{
  "type": "gameStart"
}
```



### 4. frameSync - 帧同步

服务器广播帧同步数据，包含所有玩家在该帧的输入。

**消息格式：**
```json
{
  "type": "frameSync",
  "frame": 10,
  "data": [
    {
      "id": "玩家1ID",
      "input": [1, 2, 3]
    },
    {
      "id": "玩家2ID",
      "input": [4, 5, 6]
    }
  ]
}
```



## 通信流程

1. 客户端连接WebSocket服务器
2. 服务器接受连接后，客户端发送[match](#1-match---匹配请求)消息
3. 服务器回复[matched](#1-matched---匹配中)消息表示已加入匹配队列
4. 当匹配到足够玩家时，服务器发送[matchSuccess](#2-matchsuccess---匹配成功)消息
5. 客户端收到匹配成功消息后，发送[ready](#2-ready---准备就绪)消息
6. 当所有玩家都准备就绪时，服务器发送[gameStart](#3-gamestart---游戏开始)消息
7. 游戏开始后，客户端定期发送[frameInput](#3-frameinput---帧输入数据)消息
8. 服务器收集所有玩家输入后，广播[frameSync](#4-framesync---帧同步)消息
9. 游戏持续进行，重复步骤7-8

## 错误处理

当发生错误时，连接可能会被关闭，客户端需要处理连接断开的情况并尝试重新连接。