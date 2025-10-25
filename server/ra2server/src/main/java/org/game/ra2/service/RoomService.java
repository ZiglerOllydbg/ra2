package org.game.ra2.service;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.node.ArrayNode;
import com.fasterxml.jackson.databind.node.ObjectNode;
import org.game.ra2.entity.Player;
import org.game.ra2.thread.Room;
import org.game.ra2.thread.RoomThread;

import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.LinkedBlockingQueue;

/**
 * 房间服务类
 */
public class RoomService {
    private final RoomThread roomThread;
    private final ObjectMapper objectMapper = new ObjectMapper();
    private final LinkedBlockingQueue<Message> messageQueue = new LinkedBlockingQueue<>();

    private final String roomId;
    // 房间管理数据结构
    private Room room;

    public RoomService(String roomId, RoomThread roomThread) {
        this.roomId = roomId;
        this.roomThread = roomThread;
    }

    /**
     * 添加消息到队列
     * @param channelId
     * @param data
     */
    public void addMessage(String channelId, JsonNode data) {
        try {
            System.out.println("添加房间消息到队列: " + channelId + ", 数据: " + data.toString());
            Message message = new Message(channelId, data);
            messageQueue.put(message);
        } catch (InterruptedException e) {
            e.printStackTrace();
        } catch (Exception e) {
            e.printStackTrace();
        }
    }

    /**
     * 获取房间ID
     * @return
     */
    public String getRoomId() {
        return roomId;
    }

    /**
     * 创建房间
     * @param player1
     * @param player2
     */
    public void createRoom(Player player1, Player player2) {
        // 创建房间
        room = new Room(roomId);
        room.addPlayer(player1.getChannelId(), player1.getName());
        room.addPlayer(player2.getChannelId(), player2.getName());
        
        // 在WebSocketSessionManager中也记录映射关系
        WebSocketSessionManager.getInstance().setChannelRoomMapping(player1.getChannelId(), roomId);
        WebSocketSessionManager.getInstance().setChannelRoomMapping(player2.getChannelId(), roomId);
        
        System.out.println("创建房间: " + roomId);
        
        // 通知客户端准备进入场景
        notifyMatchSuccess();
    }
    
    private void notifyMatchSuccess() {
        try {
            ObjectNode response = objectMapper.createObjectNode();
            response.put("type", "matchSuccess");
            
            ArrayNode dataArray = objectMapper.createArrayNode();
            for (Player player : room.getPlayers()) {
                ObjectNode playerNode = objectMapper.createObjectNode();
                playerNode.put("id", player.getChannelId());
                playerNode.put("name", player.getName());
                dataArray.add(playerNode);
            }
            
            response.set("data", dataArray);
            String message = objectMapper.writeValueAsString(response);
            
            System.out.println("发送匹配成功消息: " + message);
            
            // 发送给房间内的所有玩家
            for (Player player : room.getPlayers()) {
                System.out.println("向玩家发送消息: " + player.getChannelId() + ", " + player.getName());
                WebSocketSessionManager.getInstance().sendMessage(player.getChannelId(), message);
            }
        } catch (Exception e) {
            System.err.println("发送匹配成功消息时发生错误:");
            e.printStackTrace();
        }
    }

    // 删除原有的 handleReady 方法
    
    // 删除原有的 handleFrameInput 方法

    /**
     * 处理断线
     * @param channelId
     */
    public void handleDisconnect(String channelId) {
        if (room != null) {
            room.handleDisconnect(channelId);
            WebSocketSessionManager.getInstance().removeChannelRoomMapping(channelId);
            
            // 如果房间为空，通知RoomServiceManager移除RoomService
            if (room.getPlayerCount() == 0) {
                RoomServiceManager.getInstance().removeRoomService(room.getId());
            }
        }
        
        // 将断线处理任务提交给RoomThread处理
        roomThread.executeTask(() -> {
            // 这里可以添加处理断线后的逻辑
            System.out.println("在RoomThread中处理断线后的任务");
        });
    }
    
    /**
     * 停止服务
     */
    public void stopService() {
        roomThread.stopRunning();
    }
    
    /**
     * 获取房间线程
     * @return
     */
    public RoomThread getRoomThread() {
        return roomThread;
    }
    

    
    /**
     * 处理消息队列
     */
    public void processMessageQueue() {
        // 一次性获取所有消息
        List<Message> messages = new ArrayList<>();
        messageQueue.drainTo(messages);

        // 遍历所有消息，根据消息类型处理
        for (Message message : messages) {
            JsonNode data = message.getData();
            String type = data.has("type") ? data.get("type").asText() : "";
            String channelId = message.getChannelId();

            switch (type) {
                case "ready":
                    handleReadyMessage(channelId);
                    break;
                case "frameInput":
                    handleFrameInputMessage(channelId, data);
                    break;
                default:
                    // 处理其他类型的消息
                    System.out.println("房间未知消息类型: " + type);
                    break;
            }
        }
    }
    
    /**
     * 处理准备就绪消息
     * @param channelId
     */
    private void handleReadyMessage(String channelId) {
        System.out.println("=处理准备就绪后的任务");
        if (room != null) {
            room.markPlayerReady(channelId);
        }
    }

    /**
     * 处理帧输入
     * @param channelId
     * @param data
     */
    private void handleFrameInputMessage(String channelId, JsonNode data) {
        System.out.println("处理帧输入后的任务");
        if (room != null) {
            room.addFrameInput(data);
        }
    }

    public void runFrame() {
        if (room != null && room.isGameStarted()) {
            room.update();
        }
    }
}