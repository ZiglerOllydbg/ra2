package org.game.ra2;

import org.game.ra2.netty.WebSocketServer;
import org.game.ra2.service.MatchService;

/**
 * 游戏启动类
 */
public class GameStartUp {

    public static void main(String[] args) {
        try {
            // 初始化匹配服务
            MatchService matchService = MatchService.getInstance();
            
            // 启动WebSocket服务器
            WebSocketServer server = new WebSocketServer(8080, matchService);
            server.start();
        } catch (Exception e) {
            e.printStackTrace();
        }
    }
}