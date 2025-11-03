package org.game.ra2;

import org.game.ra2.netty.WebSocketServer;
import org.game.ra2.service.MatchService;
import org.apache.logging.log4j.LogManager;
import org.apache.logging.log4j.Logger;

/**
 * 游戏启动类
 */
public class GameStartUp {
    private static final Logger logger = LogManager.getLogger(GameStartUp.class);

    public static void main(String[] args) {
        try {
            // 初始化匹配服务
            MatchService matchService = MatchService.getInstance();
            
            // 启动WebSocket服务器
            WebSocketServer server = new WebSocketServer(8080, matchService);
            server.start();

            logger.info("服务器启动成功，请访问 http://localhost:8080");
            
        } catch (Exception e) {
            logger.error("服务器启动失败", e);
        }
    }
}