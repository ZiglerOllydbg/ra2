package org.game.ra2.entity;

/**
 * 玩家实体类
 */
public class Player {
    private final String channelId;
    private final String name;

    public Player(String channelId, String name) {
        this.channelId = channelId;
        this.name = name;
    }

    public String getChannelId() {
        return channelId;
    }

    public String getName() {
        return name;
    }
}