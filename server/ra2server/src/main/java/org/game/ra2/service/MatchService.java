package org.game.ra2.service;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.node.ObjectNode;
import org.game.ra2.entity.Player; // 导入独立的Player类

import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.concurrent.LinkedBlockingQueue;

/**
 * 匹配服务类
 */
public class MatchService {
    private static MatchService instance = new MatchService();
    private final LinkedBlockingQueue<Message> messageQueue = new LinkedBlockingQueue<>();
    private final List<Player> waitingPlayers = new ArrayList<>(); // 改为使用独立的Player类
    private final ObjectMapper objectMapper = new ObjectMapper();

    private MatchService() {
        // 启动匹配处理线程
        Thread matchThread = new Thread(this::run);
        matchThread.setName("MatchThread");
        matchThread.start();
    }

    public static MatchService getInstance() {
        return instance;
    }

    /**
     * 添加匹配请求到队列
     * @param channelId
     * @param data
     */
    public void addMessage(String channelId, JsonNode data) {
        try {
            System.out.println("添加匹配请求到队列: " + channelId + ", 数据: " + data.toString());
            Message message = new Message(channelId, data);
            messageQueue.put(message);

            // 返回消息matched
            Map<String, String> response = new HashMap<>();
            response.put("type", "matched");
            String jsonResponse = objectMapper.writeValueAsString(response);
            WebSocketSessionManager.getInstance().sendMessage(channelId, jsonResponse);
        } catch (InterruptedException e) {
            e.printStackTrace();
        } catch (Exception e) {
            e.printStackTrace();
        }
    }

    /**
     * 处理匹配队列
     */
    private void run() {
        System.out.println("匹配线程启动");

        while (true) {
            try {
                // 处理匹配队列中的消息
                processMessage();

                // 心跳逻辑 (每秒20帧)
                Thread.sleep(50); // 50ms = 1/20秒

            } catch (InterruptedException e) {
                Thread.currentThread().interrupt();
                break;
            } catch (Exception e) {
                e.printStackTrace();
            }
        }
    }

    private void processMessage() {
        // 一次性获取所有消息
        List<Message> messages = new ArrayList<>();
        messageQueue.drainTo(messages);

        // 遍历所有消息，根据消息类型处理
        for (Message message : messages) {
            JsonNode data = message.getData();
            String type = data.has("type") ? data.get("type").asText() : "";

            switch (type) {
                case "match":
                    handleMatchMessage(message);
                    break;
                default:
                    // 处理其他类型的消息
                    System.out.println("未知消息类型: " + type + ", message:" + message);
                    break;
            }
        }

        // 处理匹配逻辑
        processMatching();
    }

    /**
     * 处理匹配消息
     * @param message
     */
    private void handleMatchMessage(Message message) {
        // 将消息数据转换为Player并保存到等待玩家列表中
        JsonNode data = message.getData();
        String name = data.has("name") ? data.get("name").asText() : "Unknown";
        Player player = new Player(message.getChannelId(), name); // 创建独立的Player对象
        waitingPlayers.add(player);
        System.out.println("添加玩家到等待列表: " + message.getChannelId());
    }

    /**
     * 处理匹配逻辑
     */
    private void processMatching() {
        // 两两匹配创建房间
        while (waitingPlayers.size() >= 2) {
            Player player1 = waitingPlayers.remove(0);
            Player player2 = waitingPlayers.remove(0);

            System.out.println("匹配玩家: " + player1.getChannelId() + " 和 " + player2.getChannelId());

            // 创建RoomService实例来创建房间
            RoomService roomService = RoomServiceManager.getInstance().createRoomService();
            roomService.createRoom(player1, player2);
        }
    }

    /**
     * 处理断线逻辑
     * @param channelId
     */
    public void handleDisconnect(String channelId) {
        // 在匹配阶段断线，从匹配队列中移除
        // 这里可以实现更复杂的逻辑
        System.out.println("处理用户断线: " + channelId);
        
        // 从等待玩家列表中移除断线的玩家
        Player playerToRemove = null;
        for (Player player : waitingPlayers) {
            if (player.getChannelId().equals(channelId)) {
                playerToRemove = player;
                break;
            }
        }
        if (playerToRemove != null) {
            waitingPlayers.remove(playerToRemove);
        }
    }
}