package org.game.ra2.service;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;

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
    private final LinkedBlockingQueue<MatchMessage> matchQueue = new LinkedBlockingQueue<>();
    private final List<MatchMessage> waitingPlayers = new ArrayList<>();
    private final ObjectMapper objectMapper = new ObjectMapper();

    private MatchService() {
        // 启动匹配处理线程
        Thread matchThread = new Thread(this::processMatches);
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
    public void addToMatchQueue(String channelId, JsonNode data) {
        try {
            System.out.println("添加匹配请求到队列: " + channelId + ", 数据: " + data.toString());
            MatchMessage message = new MatchMessage(channelId, data);
            matchQueue.put(message);

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
    private void processMatches() {
        System.out.println("匹配线程启动");

        while (true) {
            try {
                // 处理匹配队列中的消息
                processMatchQueue();

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

    private void processMatchQueue() {
        // 处理所有排队的匹配请求
        matchQueue.drainTo(waitingPlayers);

        // 两两匹配创建房间
        while (waitingPlayers.size() >= 2) {
            MatchMessage player1 = waitingPlayers.remove(0);
            MatchMessage player2 = waitingPlayers.remove(0);

            System.out.println("匹配玩家: " + player1.getChannelId() + " 和 " + player2.getChannelId());

            // 创建房间并分配给房间线程池
            RoomService.getInstance().createRoom(player1, player2);
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
    }
}