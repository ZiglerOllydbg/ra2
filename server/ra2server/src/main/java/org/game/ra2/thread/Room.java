package org.game.ra2.thread;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import org.game.ra2.util.ObjectMapperProvider;
import com.fasterxml.jackson.databind.node.ArrayNode;
import com.fasterxml.jackson.databind.node.ObjectNode;
import org.game.ra2.entity.Player; // 使用独立的Player类
import org.game.ra2.service.WebSocketSessionManager;
import org.apache.logging.log4j.LogManager;
import org.apache.logging.log4j.Logger;

import java.util.*;
import java.util.concurrent.ConcurrentHashMap;

/**
 * 房间类
 */
public class Room {
    private static final Logger logger = LogManager.getLogger(Room.class);
    
    private final String id;
    private final List<Player> players = new ArrayList<>();
    private final Set<String> readyPlayers = new HashSet<>();
    private final Map<Integer, Map<String, JsonNode>> frameInputs = new ConcurrentHashMap<>();
    private final Map<String, String> channelIdToCampIdCache = new HashMap<>(); // 缓存channelId到campId的映射
    private int currentFrame = 0;
    private boolean gameStarted = false;
    private final ObjectMapper objectMapper = ObjectMapperProvider.getInstance();
    
    // 添加房间销毁相关字段
    private long emptySince = -1; // 房间变空的时间点
    private static final long DESTROY_DELAY = 30 * 1000; // 30秒后销毁

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
        // 更新缓存
        channelIdToCampIdCache.put(player.getChannelId(), String.valueOf(player.getCamp().getId()));
        emptySince = -1; // 有玩家加入，重置空房间计时
    }
    
    /**
     * 移除玩家
     */
    public void removePlayer(String channelId) {
        players.removeIf(player -> player.getChannelId().equals(channelId));
        // 从缓存中移除
        channelIdToCampIdCache.remove(channelId);
        readyPlayers.remove(channelId);
        
        // 检查是否所有玩家都已离开，如果是，则开始计时
        checkEmptyAndStartTimer();
    }

    /**
     * 标记玩家准备就绪
     */
    public void markPlayerReady(String channelId) {
        if (readyPlayers.contains(channelId)) {
            logger.warn("玩家 {} 已准备就绪", channelId);
            return;
        }

        logger.info("玩家 {} 准备就绪", channelId);
        readyPlayers.add(channelId);
        
        // 检查是否所有玩家都已准备就绪
        if (readyPlayers.size() == players.size() && !gameStarted) {
            startGame();
        }
    }

    private void startGame() {
        gameStarted = true;
        emptySince = -1; // 开始游戏，重置空房间计时
        
        try {
            logger.info("房间 {} 游戏开始", id);
            ObjectNode response = objectMapper.createObjectNode();
            response.put("type", "gameStart");
            
            String message = objectMapper.writeValueAsString(response);
            
            // 广播游戏开始消息
            for (Player player : players) {
                if (player.isChannelValid()) {
                    WebSocketSessionManager.getInstance().sendMessage(player.getChannelId(), message);
                } else {
                    logger.warn("玩家 {} 已断线", player);
                }
            }
        } catch (Exception e) {
            logger.error("开始游戏时发生错误", e);
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

            if (frame < currentFrame) {
                logger.warn("[{}] 收到过期的帧：{}, 当前帧={}, 数据：{}", channelId, frame, currentFrame, data.toString());
                return;
            }

            logger.info("[{}] 收到帧输入帧: {}, 当前帧：{}, 数据: {}", channelId, frame, currentFrame, data.toString());

            JsonNode inputs = data.get("data");

            Map<String, JsonNode> frameData = frameInputs.computeIfAbsent(frame, k -> new HashMap<>());

            ObjectNode playerInputs = objectMapper.createObjectNode();

            // 从缓存中获取campId
            String campId = channelIdToCampIdCache.get(channelId);
            playerInputs.put("campId", campId);
            playerInputs.set("inputs", inputs);

            frameData.put(campId, playerInputs);
        } catch (Exception e) {
            logger.error("处理帧输入时发生错误", e);
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
        
        // 检查是否所有玩家都已断线，如果是，则开始计时
        checkEmptyAndStartTimer();
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
        Map<String, JsonNode> currentFrameData = frameInputs.computeIfAbsent(currentFrame, k -> new HashMap<>());
        
        // 检查是否有所有玩家的输入数据，没有的补充空输入
        for (Player player : players) {
            String campId = String.valueOf(player.getCamp().getId());
            if (!currentFrameData.containsKey(campId)) {
                // 添加空输入
                ObjectNode emptyInput = objectMapper.createObjectNode();
                emptyInput.put("campId", campId);
                emptyInput.set("inputs", objectMapper.createArrayNode());
                currentFrameData.put(campId, emptyInput);
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
            boolean empty = true;
            ObjectNode response = objectMapper.createObjectNode();
            response.put("type", "frameSync");
            response.put("frame", frame);
            
            ArrayNode dataArray = objectMapper.createArrayNode();
            for (JsonNode input : frameData.values()) {
                dataArray.add(input);
                if (!input.get("inputs").isEmpty()) {
                    empty = false;
                }
            }
            
            response.set("data", dataArray);
            
            String message = objectMapper.writeValueAsString(response);
            
            // 发送给所有玩家
            for (Player player : players) {
                if (player.isChannelValid()) {
                    WebSocketSessionManager.getInstance().sendMessage(player.getChannelId(), message);
                }
            }

            if (!empty) {
                logger.info("房间 {} 广播帧 {} 在线人数({}) 数据：{}", id, frame, getOnlinePlayerCount(), response);
            }
        } catch (Exception e) {
            logger.error("广播帧同步数据时发生错误", e);
        }
    }

    public List<Player> getPlayers() {
        return new ArrayList<>(players);
    }

    /**
     * 在线人数
     */
    public int getOnlinePlayerCount() {
        int count = 0;
        for (Player player : players) {
            if (player.isChannelValid()) {
                count++;
            }
        }
        return count;
    }

    public int getPlayerCount() {
        return players.size();
    }
    
    public boolean isGameStarted() {
        return gameStarted;
    }
    
    /**
     * 检查房间是否为空并启动计时器
     */
    private void checkEmptyAndStartTimer() {
        boolean allDisconnected = true;
        for (Player player : players) {
            if (player.isChannelValid()) {
                allDisconnected = false;
                break;
            }
        }
        
        if (allDisconnected && emptySince == -1) {
            emptySince = System.currentTimeMillis();
            logger.info("房间 {} 所有玩家已离开，开始30秒倒计时销毁", id);
        }
    }
    
    /**
     * 检查是否应该销毁房间
     * @return true表示应该销毁房间
     */
    public boolean shouldDestroy() {
        if (emptySince != -1) {
            long currentTime = System.currentTimeMillis();
            if (currentTime - emptySince > DESTROY_DELAY) {
                logger.info("房间 {} 已空置超过30秒，准备销毁", id);
                return true;
            }
        }
        return false;
    }
}