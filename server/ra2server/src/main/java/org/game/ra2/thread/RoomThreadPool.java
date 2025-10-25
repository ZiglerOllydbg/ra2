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
        
        // 创建任务在RoomThread中执行
        Runnable task = () -> {
            // 这里可以添加房间创建后的处理逻辑
            System.out.println("在RoomThread中处理房间创建任务");
        };
        
        roomThread.executeTask(task);
    }

    public void handleReady(String roomId, String channelId) {
        // 在所有线程中广播处理准备就绪消息
        for (RoomThread roomThread : roomThreads) {
            Runnable task = () -> {
                // 这里可以添加处理准备就绪的逻辑
                System.out.println("在RoomThread中处理准备就绪消息: " + roomId + ", " + channelId);
            };
            roomThread.executeTask(task);
        }
    }

    public void handleFrameInput(String roomId, String channelId, JsonNode data) {
        // 在所有线程中广播处理帧输入
        for (RoomThread roomThread : roomThreads) {
            Runnable task = () -> {
                // 这里可以添加处理帧输入的逻辑
                System.out.println("在RoomThread中处理帧输入: " + roomId + ", " + channelId);
            };
            roomThread.executeTask(task);
        }
    }

    public void handleDisconnect(String channelId) {
        // 在所有线程中广播处理断线
        for (RoomThread roomThread : roomThreads) {
            Runnable task = () -> {
                // 这里可以添加处理断线的逻辑
                System.out.println("在RoomThread中处理断线: " + channelId);
            };
            roomThread.executeTask(task);
        }
    }

    public void handleMessage(String roomId, String channelId, JsonNode message) {
        // 在所有线程中广播处理其他消息
        for (RoomThread roomThread : roomThreads) {
            Runnable task = () -> {
                // 这里可以添加处理其他消息的逻辑
                System.out.println("在RoomThread中处理其他消息: " + roomId + ", " + channelId + ", " + message.toString());
            };
            roomThread.executeTask(task);
        }
    }
}