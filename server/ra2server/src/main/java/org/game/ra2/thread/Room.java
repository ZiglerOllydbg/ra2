package org.game.ra2.thread;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.node.ArrayNode;
import com.fasterxml.jackson.databind.node.ObjectNode;
import org.game.ra2.entity.Player; // 使用独立的Player类
import org.game.ra2.service.WebSocketSessionManager;

import java.util.*;
import java.util.concurrent.ConcurrentHashMap;

/**
 * 房间类
 */
public class Room {
    private final String id;
    private final List<Player> players = new ArrayList<>();
    private final Set<String> readyPlayers = new HashSet<>();
    private final Map<Integer, Map<String, JsonNode>> frameInputs = new ConcurrentHashMap<>();
    private int currentFrame = 0;
    private boolean gameStarted = false;
    private final ObjectMapper objectMapper = new ObjectMapper();

    public Room(String id) {
        this.id = id;
    }

    public String getId() {
        return id;
    }

    /**
     * 添加玩家
     */
    public void addPlayer(Player  player) {
        players.add(player);
    }

    /**
     * 标记玩家准备就绪
     * @param channelId
     */
    public void markPlayerReady(String channelId) {
        System.out.println("玩家 " + channelId + " 准备就绪");
        readyPlayers.add(channelId);
        
        // 检查是否所有玩家都已准备就绪
        if (readyPlayers.size() == players.size() && !gameStarted) {
            startGame();
        }
    }

    private void startGame() {
        gameStarted = true;
        
        try {
            System.out.println("房间 " + id + " 游戏开始");
            ObjectNode response = objectMapper.createObjectNode();
            response.put("type", "gameStart");
            
            String message = objectMapper.writeValueAsString(response);
            
            // 广播游戏开始消息
            for (Player player : players) {
                if (player.isChannelValid()) {
                    WebSocketSessionManager.getInstance().sendMessage(player.getChannelId(), message);
                } else {
                    System.out.println("玩家 " + player + " 已断线");
                }
            }
        } catch (Exception e) {
            e.printStackTrace();
        }
    }

    /**
     * 添加帧输入
     *
     * @param channelId
     * @param data
     */
    public void addFrameInput(String channelId, JsonNode data) {
        try {
            int frame = data.get("frame").asInt();
            JsonNode inputs = data.get("inputs");
            
            Map<String, JsonNode> frameData = new HashMap<>();
            for (JsonNode input : inputs) {
                String playerId = input.get("id").asText();
                frameData.put(playerId, input);
            }
            
            frameInputs.put(frame, frameData);
        } catch (Exception e) {
            e.printStackTrace();
        }
    }

    /**
     * 处理断线
     * @param channelId
     */
    public void handleDisconnect(String channelId) {
        // 修改ChannelValid
        for (Player player : players) {
            if (player.getChannelId().equals(channelId)) {
                player.setChannelValid(false);
            }
        }
    }

    /**
     * 房间更新逻辑（每帧调用）
     */
    public void update() {
        if (!gameStarted) {
            return;
        }

        // 处理帧同步
        processFrameSync();
        
        currentFrame++;
    }

    private void processFrameSync() {
        // 获取当前帧的数据
        Map<String, JsonNode> currentFrameData = frameInputs.getOrDefault(currentFrame, new HashMap<>());
        
        // 检查是否有所有玩家的输入数据，没有的补充空输入
        for (Player player : players) {
            String playerId = player.getChannelId();
            if (!currentFrameData.containsKey(playerId)) {
                // 添加空输入
                ObjectNode emptyInput = objectMapper.createObjectNode();
                emptyInput.put("id", playerId);
                emptyInput.set("input", objectMapper.createArrayNode());
                currentFrameData.put(playerId, emptyInput);
            }
        }
        
        // 广播帧同步数据
        broadcastFrameSync(currentFrame, currentFrameData);
        
        // 清理旧帧数据
        if (currentFrame > 10) {
            frameInputs.remove(currentFrame - 10); // 保留最近10帧数据
        }
    }
    
    private void broadcastFrameSync(int frame, Map<String, JsonNode> frameData) {
        try {
            ObjectNode response = objectMapper.createObjectNode();
            response.put("type", "frameSync");
            response.put("frame", frame);
            
            ArrayNode dataArray = objectMapper.createArrayNode();
            for (JsonNode input : frameData.values()) {
                dataArray.add(input);
            }
            
            response.set("data", dataArray);
            
            String message = objectMapper.writeValueAsString(response);
            
            // 发送给所有玩家
            for (Player player : players) {
                if (player.isChannelValid()) {
                    WebSocketSessionManager.getInstance().sendMessage(player.getChannelId(), message);
                } else {
                    System.out.println("玩家 " + player + " 已断线");
                }
            }
        } catch (Exception e) {
            e.printStackTrace();
        }
    }

    public List<Player> getPlayers() {
        return new ArrayList<>(players);
    }

    public int getPlayerCount() {
        return players.size();
    }
    
    public boolean isGameStarted() {
        return gameStarted;
    }
}