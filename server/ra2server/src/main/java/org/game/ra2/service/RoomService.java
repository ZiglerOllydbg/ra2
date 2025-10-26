package org.game.ra2.service;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.node.ArrayNode;
import com.fasterxml.jackson.databind.node.ObjectNode;
import org.game.ra2.entity.CampID;
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
    public void createRoom(MatchService.PlayerInfo player1, MatchService.PlayerInfo player2) {
        if (room != null) {
            System.err.println("房间已存在");
            return;
        }

        // 创建房间
        room = new Room(roomId);

        Player redPlayer = new Player(CampID.Red);
        redPlayer.setChannelId(player1.getChannelId());
        redPlayer.setName(player1.getName());
        room.addPlayer(redPlayer);

        Player bluePlayer = new Player(CampID.Blue);
        bluePlayer.setChannelId(player2.getChannelId());
        bluePlayer.setName(player2.getName());
        room.addPlayer(bluePlayer);
        
        // 在WebSocketSessionManager中也记录映射关系
        WebSocketSessionManager.getInstance().setChannelRoomMapping(player1.getChannelId(), roomId);
        WebSocketSessionManager.getInstance().setChannelRoomMapping(player2.getChannelId(), roomId);
        
        System.out.println("创建房间: " + roomId);

        // 发送给房间内的所有玩家
        for (Player player : room.getPlayers()) {
            if (player.isChannelValid()) {
                notifyMatchSuccess(player);
            } else {
                System.out.println("无法向玩家[" + player + "]发送匹配成功消息, 因为已断线！");
            }
        }
    }
    
    private void notifyMatchSuccess(Player sendPlayer) {
        try {
            ObjectNode response = objectMapper.createObjectNode();
            response.put("type", "matchSuccess");
            // 设置房间ID
            response.put("roomId", roomId);
            // 设置玩家campID
            response.put("yourCampId", sendPlayer.getCampId().toString());
            
            ArrayNode dataArray = objectMapper.createArrayNode();
            for (Player player : room.getPlayers()) {
                ObjectNode playerNode = objectMapper.createObjectNode();
                playerNode.put("campId", player.getCampId().toString());
                playerNode.put("name", player.getName());
                dataArray.add(playerNode);
            }
            
            response.set("data", dataArray);
            String message = objectMapper.writeValueAsString(response);

            WebSocketSessionManager.getInstance().sendMessage(sendPlayer.getChannelId(), message);

            System.out.println("向玩家[" + sendPlayer + "]发送匹配成功消息: " + message);
        } catch (Exception e) {
            System.err.println("发送匹配成功消息时发生错误:" + e.getMessage());
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
        }
    }
    
    /**
     * 停止服务
     */
    public void stopService() {
        roomThread.removeRoomService(roomId);
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
                    room.markPlayerReady(channelId);
                    break;
                case "frameInput":
                    room.addFrameInput(channelId, data);
                    break;
                default:
                    // 处理其他类型的消息
                    System.out.println("房间未知消息类型: " + type);
                    break;
            }
        }
    }

    public void pulse() {
        if (room != null && room.isGameStarted()) {
            room.update();
        }
    }
}