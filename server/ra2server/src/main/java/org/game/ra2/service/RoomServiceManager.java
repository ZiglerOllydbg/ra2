package org.game.ra2.service;

import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.atomic.AtomicInteger;

/**
 * RoomService管理器，用于管理多个RoomService实例
 */
public class RoomServiceManager {
    private static RoomServiceManager instance = new RoomServiceManager();
    
    private final List<RoomService> roomServices = new ArrayList<>();
    private final AtomicInteger currentIndex = new AtomicInteger(0);
    
    private RoomServiceManager() {
        // 初始化多个RoomService实例
        for (int i = 0; i < 4; i++) {
            RoomService roomService = new RoomService();
            roomServices.add(roomService);
        }
    }
    
    public static RoomServiceManager getInstance() {
        return instance;
    }
    
    /**
     * 轮询获取RoomService实例
     * @return RoomService实例
     */
    public RoomService getNextRoomService() {
        int index = currentIndex.getAndIncrement() % roomServices.size();
        return roomServices.get(index);
    }
    
    /**
     * 停止所有RoomService
     */
    public void stopAllServices() {
        for (RoomService roomService : roomServices) {
            roomService.stopService();
        }
    }
}