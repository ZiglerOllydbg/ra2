package org.game.ra2.thread;

import com.fasterxml.jackson.databind.ObjectMapper;
import org.game.ra2.service.MatchMessage;
import org.game.ra2.service.WebSocketSessionManager;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.node.ArrayNode;
import com.fasterxml.jackson.databind.node.ObjectNode;

import java.util.concurrent.LinkedBlockingQueue;
import java.util.concurrent.ConcurrentHashMap;
import java.util.Map;
import java.util.List;
import java.util.ArrayList;

/**
 * 房间线程类
 */
public class RoomThread extends Thread {
    private final LinkedBlockingQueue<Runnable> taskQueue = new LinkedBlockingQueue<>();
    private final ConcurrentHashMap<String, Room> rooms = new ConcurrentHashMap<>();
    private final ConcurrentHashMap<String, String> channelRoomMap = new ConcurrentHashMap<>();
    private final ObjectMapper objectMapper = new ObjectMapper();
    private volatile boolean running = true;

    public RoomThread(String name) {
        super(name);
    }

    @Override
    public void run() {
        System.out.println("房间线程启动: " + getName());

        while (running) {
            try {
                // 处理任务队列
                processTaskQueue();
                
                // 处理房间逻辑
                processRooms();
                
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

    private void processRooms() {
        // 处理房间的心跳逻辑
        for (Room room : rooms.values()) {
            room.update();
        }
    }

    /**
     * 创建房间
     * @param player1
     * @param player2
     */
    public void createRoom(MatchMessage player1, MatchMessage player2) {
        Runnable task = () -> {
            try {
                Room room = new Room(generateRoomId());
                
                // 添加玩家到房间
                room.addPlayer(player1.getChannelId(), player1.getData().get("name").asText());
                room.addPlayer(player2.getChannelId(), player2.getData().get("name").asText());
                
                // 建立channel和房间的映射关系
                channelRoomMap.put(player1.getChannelId(), room.getId());
                channelRoomMap.put(player2.getChannelId(), room.getId());
                
                // 保存房间
                rooms.put(room.getId(), room);
                
                System.out.println("创建房间: " + room.getId());
                
                // 通知客户端准备进入场景
                notifyMatchSuccess(room);
            } catch (Exception e) {
                System.err.println("创建房间时发生错误:");
                e.printStackTrace();
            }
        };
        
        try {
            taskQueue.put(task);
        } catch (InterruptedException e) {
            e.printStackTrace();
        }
    }

    private void notifyMatchSuccess(Room room) {
        try {
            ObjectNode response = objectMapper.createObjectNode();
            response.put("type", "matchSuccess");
            
            ArrayNode dataArray = objectMapper.createArrayNode();
            for (Room.Player player : room.getPlayers()) {
                ObjectNode playerNode = objectMapper.createObjectNode();
                playerNode.put("id", player.getChannelId());
                playerNode.put("name", player.getName());
                dataArray.add(playerNode);
            }
            
            response.set("data", dataArray);
            String message = objectMapper.writeValueAsString(response);
            
            System.out.println("发送匹配成功消息: " + message);
            
            // 发送给房间内的所有玩家
            for (Room.Player player : room.getPlayers()) {
                System.out.println("向玩家发送消息: " + player.getChannelId() + ", " + player.getName());
                WebSocketSessionManager.getInstance().sendMessage(player.getChannelId(), message);
            }
        } catch (Exception e) {
            System.err.println("发送匹配成功消息时发生错误:");
            e.printStackTrace();
        }
    }

    private String generateRoomId() {
        return "room_" + System.currentTimeMillis() + "_" + Thread.currentThread().getId();
    }

    public boolean containsRoom(String roomId) {
        return rooms.containsKey(roomId);
    }

    public boolean containsChannel(String channelId) {
        return channelRoomMap.containsKey(channelId);
    }

    public void handleReady(String roomId, String channelId) {
        Runnable task = () -> {
            Room room = rooms.get(roomId);
            if (room != null) {
                room.markPlayerReady(channelId);
            }
        };
        
        try {
            taskQueue.put(task);
        } catch (InterruptedException e) {
            e.printStackTrace();
        }
    }

    public void handleFrameInput(String roomId, String channelId, JsonNode data) {
        Runnable task = () -> {
            Room room = rooms.get(roomId);
            if (room != null) {
                room.addFrameInput(data);
            }
        };
        
        try {
            taskQueue.put(task);
        } catch (InterruptedException e) {
            e.printStackTrace();
        }
    }

    public void handleDisconnect(String channelId) {
        Runnable task = () -> {
            String roomId = channelRoomMap.get(channelId);
            if (roomId != null) {
                Room room = rooms.get(roomId);
                if (room != null) {
                    room.handleDisconnect(channelId);
                    channelRoomMap.remove(channelId);
                    
                    // 如果房间为空，移除房间
                    if (room.getPlayerCount() == 0) {
                        rooms.remove(roomId);
                    }
                }
            }
        };
        
        try {
            taskQueue.put(task);
        } catch (InterruptedException e) {
            e.printStackTrace();
        }
    }

    public void handleMessage(String roomId, String channelId, JsonNode message) {
        // 处理房间内的其他消息
        Runnable task = () -> {
            Room room = rooms.get(roomId);
            if (room != null) {
                // 可以在这里处理房间内的其他自定义消息
                System.out.println("处理房间内消息: " + message.toString());
            }
        };
        
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