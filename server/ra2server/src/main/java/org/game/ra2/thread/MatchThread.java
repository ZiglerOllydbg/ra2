package org.game.ra2.thread;

import com.fasterxml.jackson.databind.ObjectMapper;
import org.game.ra2.service.MatchMessage;
import org.game.ra2.service.RoomService;

import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.LinkedBlockingQueue;

/**
 * 匹配线程类
 */
public class MatchThread extends Thread {
    private final LinkedBlockingQueue<MatchMessage> matchQueue;
    private final List<MatchMessage> waitingPlayers = new ArrayList<>();
    private final ObjectMapper objectMapper = new ObjectMapper();
    private volatile boolean running = true;

    public MatchThread(LinkedBlockingQueue<MatchMessage> matchQueue) {
        this.matchQueue = matchQueue;
        this.setName("MatchThread");
    }

    @Override
    public void run() {
        System.out.println("匹配线程启动");

        while (running) {
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
            
            // 创建房间并分配给房间线程池
            RoomService.getInstance().createRoom(player1, player2);
        }
    }

    public void stopRunning() {
        running = false;
    }
}