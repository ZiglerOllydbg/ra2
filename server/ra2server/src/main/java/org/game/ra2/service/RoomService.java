package org.game.ra2.service;

import com.fasterxml.jackson.databind.JsonNode;
import org.game.ra2.service.MatchMessage;
import org.game.ra2.thread.RoomThreadPool;

/**
 * 房间服务类
 */
public class RoomService {
    private static RoomService instance = new RoomService();
    private final RoomThreadPool roomThreadPool;

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
        roomThreadPool.assignRoom(player1, player2);
    }

    /**
     * 处理准备就绪消息
     * @param roomId
     * @param channelId
     */
    public void handleReady(String roomId, String channelId) {
        roomThreadPool.handleReady(roomId, channelId);
    }

    /**
     * 处理帧输入
     * @param roomId
     * @param channelId
     * @param data
     */
    public void handleFrameInput(String roomId, String channelId, JsonNode data) {
        roomThreadPool.handleFrameInput(roomId, channelId, data);
    }

    /**
     * 处理断线
     * @param channelId
     */
    public void handleDisconnect(String channelId) {
        roomThreadPool.handleDisconnect(channelId);
    }

    /**
     * 处理其他消息
     * @param roomId
     * @param channelId
     * @param message
     */
    public void handleMessage(String roomId, String channelId, JsonNode message) {
        roomThreadPool.handleMessage(roomId, channelId, message);
    }
}