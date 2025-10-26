package org.game.ra2.entity;

public enum CampID {
    Red(1),
    Blue(2);

    private final int id;
    CampID(int id) {
        this.id = id;
    }
    public int getId() {
        return id;
    }

    public static CampID fromId(int id) {
        for (CampID campID : values()) {
            if (campID.id == id) {
                return campID;
            }
        }
        return null;
    }
}
