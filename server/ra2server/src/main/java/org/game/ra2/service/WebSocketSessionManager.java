package org.game.ra2.service;

import io.netty.channel.Channel;
import io.netty.channel.ChannelFuture;
import io.netty.channel.ChannelFutureListener;
import io.netty.handler.codec.http.websocketx.TextWebSocketFrame;

import java.util.concurrent.ConcurrentHashMap;

/**
 * WebSocket会话管理器
 */
public class WebSocketSessionManager {
    private static WebSocketSessionManager instance = new WebSocketSessionManager();
    private final ConcurrentHashMap<String, Channel> channels = new ConcurrentHashMap<>();
    private final ConcurrentHashMap<String, String> channelRoomMap = new ConcurrentHashMap<>();

    private WebSocketSessionManager() {
    }

    public static WebSocketSessionManager getInstance() {
        return instance;
    }

    public void addChannel(Channel channel) {
        channels.put(channel.id().asLongText(), channel);
    }

    public void removeChannel(String channelId) {
        channels.remove(channelId);
        channelRoomMap.remove(channelId);
    }

    public Channel getChannel(String channelId) {
        return channels.get(channelId);
    }

    /**
     * 线程安全的消息发送方法
     * @param channelId
     * @param message
     */
    public void sendMessage(String channelId, String message) {
        Channel channel = channels.get(channelId);
        if (channel != null && channel.isActive()) {
            channel.eventLoop().execute(() -> {
                channel.writeAndFlush(new TextWebSocketFrame(message)).addListener((ChannelFutureListener) future -> {
                    if (!future.isSuccess()) {
                        System.err.println("消息发送失败 - 频道ID: " + channelId + ", 消息: " + message);
                        future.cause().printStackTrace();
                    }
                });
            });
        } else {
            System.out.println("无法发送消息到频道: " + channelId + ", 频道状态: " + (channel != null ? "活跃=" + channel.isActive() : "不存在"));
        }
    }
    
    /**
     * 设置频道与房间的映射关系
     * @param channelId 频道ID
     * @param roomId 房间ID
     */
    public void setChannelRoomMapping(String channelId, String roomId) {
        channelRoomMap.put(channelId, roomId);
    }
    
    /**
     * 获取频道所属的房间ID
     * @param channelId 频道ID
     * @return 房间ID
     */
    public String getRoomIdByChannel(String channelId) {
        return channelRoomMap.get(channelId);
    }

    /**
     * channel存在房间
     */
    public boolean isChannelInRoom(String channelId) {
        return channelRoomMap.containsKey(channelId);
    }

    /**
     * 移除频道与房间的映射关系
     * @param channelId 频道ID
     */
    public void removeChannelRoomMapping(String channelId) {
        channelRoomMap.remove(channelId);
    }
}