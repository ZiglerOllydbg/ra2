package org.game.ra2.thread;

import org.game.ra2.service.RoomService;

import java.util.HashMap;
import java.util.Map;
import java.util.concurrent.LinkedBlockingQueue;
import java.util.List;
import java.util.ArrayList;

/**
 * 房间线程类
 */
public class RoomThread extends Thread {
    private final LinkedBlockingQueue<Runnable> taskQueue = new LinkedBlockingQueue<>();
    private volatile boolean running = true;
    private Map<String, RoomService> roomServices = new HashMap<>();

    public RoomThread(String name) {
        super(name);
    }
    
    public void addRoomService(RoomService roomService) {
        this.roomServices.put(roomService.getRoomId(), roomService);
    }

    @Override
    public void run() {
        System.out.println("房间线程启动: " + getName());

        while (running) {
            try {
                // 处理任务队列
                processTaskQueue();
                
                // 处理房间消息队列
                for (RoomService roomService : roomServices.values()) {
                    roomService.processMessageQueue();
                    roomService.runFrame();
                }

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

    private void processTaskQueue() {
        List<Runnable> tasks = new ArrayList<>();
        taskQueue.drainTo(tasks);
        
        for (Runnable task : tasks) {
            try {
                task.run();
            } catch (Exception e) {
                e.printStackTrace();
            }
        }
    }

    /**
     * 执行任务
     * @param task
     */
    public void executeTask(Runnable task) {
        try {
            taskQueue.put(task);
        } catch (InterruptedException e) {
            e.printStackTrace();
        }
    }

    public void stopRunning() {
        running = false;
    }
}