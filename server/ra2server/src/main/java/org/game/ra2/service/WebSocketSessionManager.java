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
                    if (future.isSuccess()) {
                        System.out.println("消息发送成功 - 频道ID: " + channelId + ", 消息: " + message);
                    } else {
                        System.err.println("消息发送失败 - 频道ID: " + channelId + ", 消息: " + message);
                        future.cause().printStackTrace();
                    }
                });
            });
        } else {
            System.out.println("无法发送消息到频道: " + channelId + ", 频道状态: " + (channel != null ? "活跃=" + channel.isActive() : "不存在"));
        }
    }
}