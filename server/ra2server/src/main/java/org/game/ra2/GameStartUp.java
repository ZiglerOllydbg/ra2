package org.game.ra2;

import java.time.LocalDateTime;
import java.time.format.DateTimeFormatter;
import java.util.Timer;
import java.util.TimerTask;

/**
 * 游戏启动类
 */
public class GameStartUp {

    public static void main(String[] args) {
        Timer timer = new Timer();
        DateTimeFormatter formatter = DateTimeFormatter.ofPattern("yyyy-MM-dd HH:mm:ss");
        
        timer.scheduleAtFixedRate(new TimerTask() {
            @Override
            public void run() {
                System.out.println(LocalDateTime.now().format(formatter));
            }
        }, 0, 1000);
    }
}