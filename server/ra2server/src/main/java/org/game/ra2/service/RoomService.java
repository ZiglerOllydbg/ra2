package org.game.ra2.service;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.node.ArrayNode;
import com.fasterxml.jackson.databind.node.ObjectNode;
import org.game.ra2.thread.Room;
import org.game.ra2.thread.RoomThread;

import java.util.Map;
import java.util.concurrent.ConcurrentHashMap;

/**
 * 房间服务类
 */
public class RoomService {
    private final RoomThread roomThread;
    private final ObjectMapper objectMapper = new ObjectMapper();
    
    // 房间管理数据结构
    private Room room;
    private final Map<String, String> channelRoomMap = new ConcurrentHashMap<>();

    public RoomService(RoomThread roomThread) {
        this.roomThread = roomThread;
    }

    /**
     * 创建房间
     * @param player1
     * @param player2
     */
    public void createRoom(MatchMessage player1, MatchMessage player2) {
        // 生成房间ID
        String roomId = "room_" + System.currentTimeMillis();
        
        // 创建房间
        room = new Room(roomId);
        room.addPlayer(player1.getChannelId(), player1.getData().get("name").asText());
        room.addPlayer(player2.getChannelId(), player2.getData().get("name").asText());
        
        // 建立channel和房间的映射关系
        channelRoomMap.put(player1.getChannelId(), roomId);
        channelRoomMap.put(player2.getChannelId(), roomId);
        
        // 在WebSocketSessionManager中也记录映射关系
        WebSocketSessionManager.getInstance().setChannelRoomMapping(player1.getChannelId(), roomId);
        WebSocketSessionManager.getInstance().setChannelRoomMapping(player2.getChannelId(), roomId);
        
        System.out.println("创建房间: " + roomId);
        
        // 通知客户端准备进入场景
        notifyMatchSuccess();
        
        // 将房间创建任务提交给RoomThread处理
        roomThread.executeTask(() -> {
            // 这里可以添加房间创建后的处理逻辑
            System.out.println("在RoomThread中处理房间创建后的任务");
        });
    }
    
    private void notifyMatchSuccess() {
        try {
            ObjectNode response = objectMapper.createObjectNode();
            response.put("type", "matchSuccess");
            
            ArrayNode dataArray = objectMapper.createArrayNode();
            for (Room.Player player : room.getPlayers()) {
                ObjectNode playerNode = objectMapper.createObjectNode();
                playerNode.put("id", player.getChannelId());
                playerNode.put("name", player.getName());
                dataArray.add(playerNode);
            }
            
            response.set("data", dataArray);
            String message = objectMapper.writeValueAsString(response);
            
            System.out.println("发送匹配成功消息: " + message);
            
            // 发送给房间内的所有玩家
            for (Room.Player player : room.getPlayers()) {
                System.out.println("向玩家发送消息: " + player.getChannelId() + ", " + player.getName());
                WebSocketSessionManager.getInstance().sendMessage(player.getChannelId(), message);
            }
        } catch (Exception e) {
            System.err.println("发送匹配成功消息时发生错误:");
            e.printStackTrace();
        }
    }

    /**
     * 处理准备就绪消息
     * @param channelId
     */
    public void handleReady(String channelId) {
        if (room != null) {
            room.markPlayerReady(channelId);
        }
        
        // 将准备就绪任务提交给RoomThread处理
        roomThread.executeTask(() -> {
            // 这里可以添加处理准备就绪后的逻辑
            System.out.println("在RoomThread中处理准备就绪后的任务");
        });
    }

    /**
     * 处理帧输入
     * @param channelId
     * @param data
     */
    public void handleFrameInput(String channelId, JsonNode data) {
        if (room != null) {
            room.addFrameInput(data);
        }
        
        // 将帧输入任务提交给RoomThread处理
        roomThread.executeTask(() -> {
            // 这里可以添加处理帧输入后的逻辑
            System.out.println("在RoomThread中处理帧输入后的任务");
        });
    }

    /**
     * 处理断线
     * @param channelId
     */
    public void handleDisconnect(String channelId) {
        if (room != null) {
            room.handleDisconnect(channelId);
            channelRoomMap.remove(channelId);
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
     * 处理其他消息
     * @param channelId
     * @param message
     */
    public void handleMessage(String channelId, JsonNode message) {
        // 可以在这里处理房间内的其他自定义消息
        System.out.println("处理房间内消息: " + message.toString());
        
        // 将消息处理任务提交给RoomThread处理
        roomThread.executeTask(() -> {
            // 这里可以添加处理消息后的逻辑
            System.out.println("在RoomThread中处理消息后的任务");
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
     * 获取房间ID
     * @return
     */
    public String getRoomId() {
        return room != null ? room.getId() : null;
    }
}