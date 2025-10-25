package org.game.ra2.service;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import org.game.ra2.thread.MatchThread;

import java.util.HashMap;
import java.util.Map;
import java.util.concurrent.LinkedBlockingQueue;

/**
 * 匹配服务类
 */
public class MatchService {
    private static MatchService instance = new MatchService();
    private final MatchThread matchThread;
    private final LinkedBlockingQueue<MatchMessage> matchQueue = new LinkedBlockingQueue<>();
    private final ObjectMapper objectMapper = new ObjectMapper();

    private MatchService() {
        matchThread = new MatchThread(matchQueue);
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
     * 处理断线逻辑
     * @param channelId
     */
    public void handleDisconnect(String channelId) {
        // 在匹配阶段断线，从匹配队列中移除
        // 这里可以实现更复杂的逻辑
        System.out.println("处理用户断线: " + channelId);
    }
}