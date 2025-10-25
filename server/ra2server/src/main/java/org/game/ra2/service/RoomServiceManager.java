package org.game.ra2.service;

import org.game.ra2.thread.RoomThread;

import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.atomic.AtomicInteger;

/**
 * RoomService管理器，用于管理多个RoomService实例
 */
public class RoomServiceManager {
    private static RoomServiceManager instance = new RoomServiceManager();
    
    private final List<RoomThread> roomThreads = new ArrayList<>();
    private final AtomicInteger currentIndex = new AtomicInteger(0);
    private final ConcurrentHashMap<String, RoomService> roomServices = new ConcurrentHashMap<>();
    
    private RoomServiceManager() {
        // 初始化多个RoomThread实例
        for (int i = 0; i < 4; i++) {
            RoomThread roomThread = new RoomThread("RoomThread-" + i);
            roomThreads.add(roomThread);
            roomThread.start();
        }
    }
    
    public static RoomServiceManager getInstance() {
        return instance;
    }
    
    /**
     * 创建一个新的RoomService并分配给一个RoomThread
     * @param roomId 房间ID
     * @return RoomService实例
     */
    public RoomService createRoomService(String roomId) {
        int index = currentIndex.getAndIncrement() % roomThreads.size();
        RoomThread roomThread = roomThreads.get(index);
        RoomService roomService = new RoomService(roomThread);
        roomServices.put(roomId, roomService);
        return roomService;
    }
    
    /**
     * 根据房间ID获取RoomService
     * @param roomId 房间ID
     * @return RoomService实例
     */
    public RoomService getRoomService(String roomId) {
        return roomServices.get(roomId);
    }
    
    /**
     * 移除RoomService
     * @param roomId 房间ID
     */
    public void removeRoomService(String roomId) {
        RoomService roomService = roomServices.remove(roomId);
        if (roomService != null) {
            roomService.stopService();
        }
    }
    
    /**
     * 停止所有RoomService
     */
    public void stopAllServices() {
        for (RoomThread roomThread : roomThreads) {
            roomThread.stopRunning();
        }
        roomServices.clear();
    }
}