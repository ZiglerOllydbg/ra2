package org.game.ra2.service;

import com.fasterxml.jackson.databind.JsonNode;

/**
 * 匹配消息类
 */
public class MatchMessage {
    private final String channelId;
    private final JsonNode data;

    public MatchMessage(String channelId, JsonNode data) {
        this.channelId = channelId;
        this.data = data;
    }

    public String getChannelId() {
        return channelId;
    }

    public JsonNode getData() {
        return data;
    }
}