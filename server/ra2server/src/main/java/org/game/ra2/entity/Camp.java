package org.game.ra2.entity;

public enum Camp {
    Red(1),
    Blue(2);

    private final int id;
    Camp(int id) {
        this.id = id;
    }
    public int getId() {
        return id;
    }

    public static Camp fromId(int id) {
        for (Camp camp : values()) {
            if (camp.id == id) {
                return camp;
            }
        }
        return null;
    }
}
