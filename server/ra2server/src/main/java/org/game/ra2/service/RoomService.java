package org.game.ra2.service;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.node.ArrayNode;
import com.fasterxml.jackson.databind.node.ObjectNode;
import org.game.ra2.thread.RoomThreadPool;

import java.util.ArrayList;
import java.util.HashSet;
import java.util.List;
import java.util.Set;
import java.util.Map;
import java.util.HashMap;
import java.util.concurrent.ConcurrentHashMap;

/**
 * 房间服务类
 */
public class RoomService {
    private static RoomService instance = new RoomService();
    private final RoomThreadPool roomThreadPool;
    private final ObjectMapper objectMapper = new ObjectMapper();
    
    // 房间管理数据结构
    private final Map<String, RoomInfo> rooms = new ConcurrentHashMap<>();
    private final Map<String, String> channelRoomMap = new ConcurrentHashMap<>();

    private RoomService() {
        roomThreadPool = new RoomThreadPool(4); // 创建4个房间线程
    }

    public static RoomService getInstance() {
        return instance;
    }

    /**
     * 创建房间
     * @param player1
     * @param player2
     */
    public void createRoom(MatchMessage player1, MatchMessage player2) {
        // 生成房间ID
        String roomId = generateRoomId();
        
        // 创建房间信息
        RoomInfo roomInfo = new RoomInfo(roomId);
        roomInfo.addPlayer(player1.getChannelId(), player1.getData().get("name").asText());
        roomInfo.addPlayer(player2.getChannelId(), player2.getData().get("name").asText());
        
        // 建立channel和房间的映射关系
        channelRoomMap.put(player1.getChannelId(), roomId);
        channelRoomMap.put(player2.getChannelId(), roomId);
        
        // 保存房间
        rooms.put(roomId, roomInfo);
        
        System.out.println("创建房间: " + roomId);
        
        // 通知客户端准备进入场景
        notifyMatchSuccess(roomInfo);
        
        // 将房间分配给线程池处理
        roomThreadPool.assignRoom(player1, player2);
    }
    
    private void notifyMatchSuccess(RoomInfo room) {
        try {
            ObjectNode response = objectMapper.createObjectNode();
            response.put("type", "matchSuccess");
            
            ArrayNode dataArray = objectMapper.createArrayNode();
            for (PlayerInfo player : room.getPlayers()) {
                ObjectNode playerNode = objectMapper.createObjectNode();
                playerNode.put("id", player.getChannelId());
                playerNode.put("name", player.getName());
                dataArray.add(playerNode);
            }
            
            response.set("data", dataArray);
            String message = objectMapper.writeValueAsString(response);
            
            System.out.println("发送匹配成功消息: " + message);
            
            // 发送给房间内的所有玩家
            for (PlayerInfo player : room.getPlayers()) {
                System.out.println("向玩家发送消息: " + player.getChannelId() + ", " + player.getName());
                WebSocketSessionManager.getInstance().sendMessage(player.getChannelId(), message);
            }
        } catch (Exception e) {
            System.err.println("发送匹配成功消息时发生错误:");
            e.printStackTrace();
        }
    }
    
    private String generateRoomId() {
        return "room_" + System.currentTimeMillis();
    }

    /**
     * 处理准备就绪消息
     * @param roomId
     * @param channelId
     */
    public void handleReady(String roomId, String channelId) {
        RoomInfo room = rooms.get(roomId);
        if (room != null) {
            room.markPlayerReady(channelId);
            
            // 检查是否所有玩家都已准备就绪
            if (room.allPlayersReady() && !room.isGameStarted()) {
                startGame(roomId);
            }
        }
        
        roomThreadPool.handleReady(roomId, channelId);
    }
    
    private void startGame(String roomId) {
        RoomInfo room = rooms.get(roomId);
        if (room != null) {
            room.setGameStarted(true);
            
            try {
                System.out.println("房间 " + roomId + " 游戏开始");
                ObjectNode response = objectMapper.createObjectNode();
                response.put("type", "gameStart");
                
                String message = objectMapper.writeValueAsString(response);
                
                // 广播游戏开始消息
                for (PlayerInfo player : room.getPlayers()) {
                    WebSocketSessionManager.getInstance().sendMessage(player.getChannelId(), message);
                }
            } catch (Exception e) {
                e.printStackTrace();
            }
        }
    }

    /**
     * 处理帧输入
     * @param roomId
     * @param channelId
     * @param data
     */
    public void handleFrameInput(String roomId, String channelId, JsonNode data) {
        RoomInfo room = rooms.get(roomId);
        if (room != null) {
            try {
                int frame = data.get("frame").asInt();
                JsonNode inputs = data.get("inputs");
                
                Map<String, JsonNode> frameData = room.getFrameInputs().computeIfAbsent(frame, k -> new HashMap<>());
                for (JsonNode input : inputs) {
                    String playerId = input.get("id").asText();
                    frameData.put(playerId, input);
                }
            } catch (Exception e) {
                e.printStackTrace();
            }
        }
        
        roomThreadPool.handleFrameInput(roomId, channelId, data);
    }

    /**
     * 处理断线
     * @param channelId
     */
    public void handleDisconnect(String channelId) {
        String roomId = channelRoomMap.get(channelId);
        if (roomId != null) {
            RoomInfo room = rooms.get(roomId);
            if (room != null) {
                room.removePlayer(channelId);
                channelRoomMap.remove(channelId);
                
                // 如果房间为空，移除房间
                if (room.getPlayerCount() == 0) {
                    rooms.remove(roomId);
                }
            }
        }
        
        roomThreadPool.handleDisconnect(channelId);
    }

    /**
     * 处理其他消息
     * @param roomId
     * @param channelId
     * @param message
     */
    public void handleMessage(String roomId, String channelId, JsonNode message) {
        // 可以在这里处理房间内的其他自定义消息
        System.out.println("处理房间内消息: " + message.toString());
        
        roomThreadPool.handleMessage(roomId, channelId, message);
    }
    
    /**
     * 房间信息内部类
     */
    private static class RoomInfo {
        private final String id;
        private final List<PlayerInfo> players = new ArrayList<>();
        private final Set<String> readyPlayers = new HashSet<>();
        private final Map<Integer, Map<String, JsonNode>> frameInputs = new ConcurrentHashMap<>();
        private boolean gameStarted = false;
        
        public RoomInfo(String id) {
            this.id = id;
        }
        
        public String getId() {
            return id;
        }
        
        public void addPlayer(String channelId, String name) {
            players.add(new PlayerInfo(channelId, name));
        }
        
        public void removePlayer(String channelId) {
            players.removeIf(player -> player.getChannelId().equals(channelId));
            readyPlayers.remove(channelId);
        }
        
        public void markPlayerReady(String channelId) {
            System.out.println("玩家 " + channelId + " 准备就绪");
            readyPlayers.add(channelId);
        }
        
        public boolean allPlayersReady() {
            return readyPlayers.size() == players.size();
        }
        
        public List<PlayerInfo> getPlayers() {
            return new ArrayList<>(players);
        }
        
        public int getPlayerCount() {
            return players.size();
        }
        
        public boolean isGameStarted() {
            return gameStarted;
        }
        
        public void setGameStarted(boolean gameStarted) {
            this.gameStarted = gameStarted;
        }
        
        public Map<Integer, Map<String, JsonNode>> getFrameInputs() {
            return frameInputs;
        }
    }
    
    /**
     * 玩家信息内部类
     */
    private static class PlayerInfo {
        private final String channelId;
        private final String name;
        
        public PlayerInfo(String channelId, String name) {
            this.channelId = channelId;
            this.name = name;
        }
        
        public String getChannelId() {
            return channelId;
        }
        
        public String getName() {
            return name;
        }
    }
}