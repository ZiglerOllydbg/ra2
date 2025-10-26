package org.game.ra2.entity;

import org.apache.commons.lang3.builder.ToStringBuilder;

/**
 * 玩家实体类
 */
public class Player {
    /**
     * 阵营ID
     */
    private final CampID campId;
    /**
     * 频道ID
     */
    private String channelId;
    /**
     * 玩家名称
     */
    private String name;
    /**
     * 是否有效
     */
    private boolean channelValid;
    /**
     * token
     */
    private String token;

    public Player(CampID campId) {
        this.campId = campId;
        this.channelValid = true;
        // 随机token
        this.token = String.valueOf(System.currentTimeMillis());
    }

    public CampID getCampId() {
        return campId;
    }

    public String getChannelId() {
        return channelId;
    }

    public void setChannelId(String channelId) {
        this.channelId = channelId;
    }

    public void setName(String name) {
        this.name = name;
    }

    public String getName() {
        return name;
    }

    public boolean isChannelValid() {
        return channelValid;
    }

    public void setChannelValid(boolean channelValid) {
        this.channelValid = channelValid;
    }

    public String getToken() {
        return token;
    }

    @Override
    public String toString() {
        return new ToStringBuilder(this)
                .append("campId", campId)
                .append("channelId", channelId)
                .append("name", name)
                .toString();
    }
}