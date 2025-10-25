package org.game.ra2.netty;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import io.netty.channel.ChannelHandlerContext;
import io.netty.channel.SimpleChannelInboundHandler;
import io.netty.handler.codec.http.websocketx.TextWebSocketFrame;
import io.netty.handler.codec.http.websocketx.WebSocketServerProtocolHandler;
import org.game.ra2.service.MatchService;
import org.game.ra2.service.RoomService;
import org.game.ra2.service.WebSocketSessionManager;

import java.util.concurrent.ConcurrentHashMap;

public class WebSocketFrameHandler extends SimpleChannelInboundHandler<TextWebSocketFrame> {

    private final ObjectMapper objectMapper = new ObjectMapper();
    private final MatchService matchService;
    
    // 临时存储session信息，后续会移到专门的管理器中
    public static final ConcurrentHashMap<String, String> CHANNEL_ROOM_MAP = new ConcurrentHashMap<>();

    public WebSocketFrameHandler(MatchService matchService) {
        this.matchService = matchService;
    }

    @Override
    public void handlerAdded(ChannelHandlerContext ctx) throws Exception {
        WebSocketSessionManager.getInstance().addChannel(ctx.channel());
        System.out.println("新的连接加入: " + ctx.channel().id().asLongText());
    }

    @Override
    public void handlerRemoved(ChannelHandlerContext ctx) throws Exception {
        String channelId = ctx.channel().id().asLongText();
        WebSocketSessionManager.getInstance().removeChannel(channelId);
        
        System.out.println("连接断开: " + channelId);
        
        // 处理断线逻辑
        if (CHANNEL_ROOM_MAP.containsKey(channelId)) {
            // 如果在房间中，则交给房间线程处理
            System.out.println("房间中的玩家断开: " + channelId);
            RoomService.getInstance().handleDisconnect(channelId);
            CHANNEL_ROOM_MAP.remove(channelId);
        } else {
            // 如果不在房间中，交给匹配线程处理
            System.out.println("匹配中的玩家断开: " + channelId);
            matchService.handleDisconnect(channelId);
        }
    }

    @Override
    public void userEventTriggered(ChannelHandlerContext ctx, Object evt) throws Exception {
        if (evt instanceof WebSocketServerProtocolHandler.HandshakeComplete) {
            System.out.println("WebSocket握手完成");
        } else {
            super.userEventTriggered(ctx, evt);
        }
    }

    @Override
    protected void channelRead0(ChannelHandlerContext ctx, TextWebSocketFrame msg) throws Exception {
        String channelId = ctx.channel().id().asLongText();
        String request = msg.text();
        
        System.out.println("收到消息: " + request);
        
        try {
            JsonNode jsonNode = objectMapper.readTree(request);
            String type = jsonNode.get("type").asText();
            
            switch (type) {
                case "match":
                    // 添加到匹配队列
                    matchService.addToMatchQueue(channelId, jsonNode.get("data"));
                    break;
                case "ready":
                    // 准备就绪消息
                    if (CHANNEL_ROOM_MAP.containsKey(channelId)) {
                        String roomId = CHANNEL_ROOM_MAP.get(channelId);
                        RoomService.getInstance().handleReady(roomId, channelId);
                    }
                    break;
                case "frameInput":
                    // 帧输入消息
                    if (CHANNEL_ROOM_MAP.containsKey(channelId)) {
                        String roomId = CHANNEL_ROOM_MAP.get(channelId);
                        RoomService.getInstance().handleFrameInput(roomId, channelId, jsonNode.get("data"));
                    }
                    break;
                default:
                    // 其他消息根据房间信息转发
                    if (CHANNEL_ROOM_MAP.containsKey(channelId)) {
                        String roomId = CHANNEL_ROOM_MAP.get(channelId);
                        RoomService.getInstance().handleMessage(roomId, channelId, jsonNode);
                    }
                    break;
            }
        } catch (Exception e) {
            System.err.println("处理消息时发生错误: " + request);
            e.printStackTrace();
        }
    }
}