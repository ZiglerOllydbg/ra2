package org.game.ra2.thread;

import com.fasterxml.jackson.databind.JsonNode;
import org.game.ra2.service.MatchMessage;

import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.atomic.AtomicInteger;

/**
 * 房间线程池
 */
public class RoomThreadPool {
    private final List<RoomThread> roomThreads;
    private final AtomicInteger index = new AtomicInteger(0);

    public RoomThreadPool(int threadCount) {
        roomThreads = new ArrayList<>();
        for (int i = 0; i < threadCount; i++) {
            RoomThread roomThread = new RoomThread("RoomThread-" + i);
            roomThread.start();
            roomThreads.add(roomThread);
        }
    }

    /**
     * 分配房间给线程
     * @param player1
     * @param player2
     */
    public void assignRoom(MatchMessage player1, MatchMessage player2) {
        // 轮询分配到不同的房间线程
        int currentIndex = index.getAndIncrement() % roomThreads.size();
        RoomThread roomThread = roomThreads.get(currentIndex);
        roomThread.createRoom(player1, player2);
    }

    public void handleReady(String roomId, String channelId) {
        // 找到对应的房间线程处理
        for (RoomThread roomThread : roomThreads) {
            if (roomThread.containsRoom(roomId)) {
                roomThread.handleReady(roomId, channelId);
                break;
            }
        }
    }

    public void handleFrameInput(String roomId, String channelId, JsonNode data) {
        // 找到对应的房间线程处理
        for (RoomThread roomThread : roomThreads) {
            if (roomThread.containsRoom(roomId)) {
                roomThread.handleFrameInput(roomId, channelId, data);
                break;
            }
        }
    }

    public void handleDisconnect(String channelId) {
        // 找到对应的房间线程处理
        for (RoomThread roomThread : roomThreads) {
            if (roomThread.containsChannel(channelId)) {
                roomThread.handleDisconnect(channelId);
                break;
            }
        }
    }

    public void handleMessage(String roomId, String channelId, JsonNode message) {
        // 找到对应的房间线程处理
        for (RoomThread roomThread : roomThreads) {
            if (roomThread.containsRoom(roomId)) {
                roomThread.handleMessage(roomId, channelId, message);
                break;
            }
        }
    }
}