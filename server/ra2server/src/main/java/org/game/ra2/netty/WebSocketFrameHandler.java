package org.game.ra2.netty;

import com.fasterxml.jackson.databind.JsonNode;
import io.netty.channel.ChannelFutureListener;
import io.netty.channel.ChannelHandlerContext;
import io.netty.channel.SimpleChannelInboundHandler;
import io.netty.handler.codec.http.websocketx.TextWebSocketFrame;
import io.netty.handler.codec.http.websocketx.WebSocketServerProtocolHandler;
import org.apache.logging.log4j.LogManager;
import org.apache.logging.log4j.Logger;
import org.game.ra2.service.MatchService;
import org.game.ra2.service.RoomService;
import org.game.ra2.service.RoomServiceManager;
import org.game.ra2.service.WebSocketSessionManager;
import org.game.ra2.util.ObjectMapperProvider;

import java.util.HashMap;
import java.util.Map;

public class WebSocketFrameHandler extends SimpleChannelInboundHandler<TextWebSocketFrame> {
    private static final Logger logger = LogManager.getLogger(WebSocketFrameHandler.class);

    private final MatchService matchService;

    public WebSocketFrameHandler(MatchService matchService) {
        this.matchService = matchService;
    }

    @Override
    public void handlerAdded(ChannelHandlerContext ctx) throws Exception {
        WebSocketSessionManager.getInstance().addChannel(ctx.channel());
        logger.info("新的连接加入: {}", ctx.channel().id().asLongText());
    }

    @Override
    public void handlerRemoved(ChannelHandlerContext ctx) throws Exception {
        String channelId = ctx.channel().id().asLongText();

        logger.info("连接断开: {}", channelId);
        
        // 处理断线逻辑
        String roomId = WebSocketSessionManager.getInstance().getRoomIdByChannel(channelId);
        if (roomId != null) {
            // 如果在房间中，则交给房间线程处理
            logger.info("房间中的玩家断开: {}", channelId);
            RoomService roomService = RoomServiceManager.getInstance().getRoomService(roomId);
            if (roomService != null) {
                roomService.handleDisconnect(channelId);
            }
            WebSocketSessionManager.getInstance().removeChannelRoomMapping(channelId);
        } else {
            // 如果不在房间中，交给匹配线程处理
            logger.info("匹配中的玩家断开: {}", channelId);
            matchService.handleDisconnect(channelId);
        }

        WebSocketSessionManager.getInstance().removeChannel(channelId);
    }

    @Override
    public void userEventTriggered(ChannelHandlerContext ctx, Object evt) throws Exception {
        if (evt instanceof WebSocketServerProtocolHandler.HandshakeComplete) {
            logger.info("WebSocket握手完成");
        } else {
            super.userEventTriggered(ctx, evt);
        }
    }

    @Override
    protected void channelRead0(ChannelHandlerContext ctx, TextWebSocketFrame msg) throws Exception {
        String channelId = ctx.channel().id().asLongText();
        String request = msg.text();
        
        try {
            JsonNode jsonNode = ObjectMapperProvider.getInstance().readTree(request);
            String type = jsonNode.get("type").asText();

            if (type.equals("ping")) { // 处理ping消息，返回pong
                Map<String, String> pongResponse = new HashMap<>();
                pongResponse.put("type", "pong");
                String jsonResponse = ObjectMapperProvider.getInstance().writeValueAsString(pongResponse);
                ctx.channel().writeAndFlush(new TextWebSocketFrame(jsonResponse)).addListener((ChannelFutureListener) future -> {
                    if (!future.isSuccess()) {
                        logger.error("发送pong消息失败 - channelId: {}", channelId, future.cause());
                    }
                });
            } else if (type.equals("match")) {// 添加到匹配队列
                matchService.addMessage(channelId, jsonNode);
            } else {// 其他消息根据房间信息转发
                String roomId = WebSocketSessionManager.getInstance().getRoomIdByChannel(channelId);
                if (roomId != null) {
                    RoomService roomService = RoomServiceManager.getInstance().getRoomService(roomId);
                    if (roomService != null) {
                        roomService.addMessage(channelId, jsonNode);
                    } else {
                        logger.error("房间不存在: {}", roomId);
                    }
                } else {
                    logger.error("玩家未加入房间: {}", channelId);
                }
            }
        } catch (Exception e) {
            logger.error("处理消息时发生错误: {}", request, e);
        }
    }
}