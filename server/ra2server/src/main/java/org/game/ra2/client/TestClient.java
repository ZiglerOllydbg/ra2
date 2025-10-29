package org.game.ra2.client;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.node.ArrayNode;
import com.fasterxml.jackson.databind.node.ObjectNode;
import org.game.ra2.util.ObjectMapperProvider;
import org.java_websocket.client.WebSocketClient;
import org.java_websocket.handshake.ServerHandshake;

import java.net.URI;
import java.util.Random;
import java.util.Scanner;

public class TestClient extends WebSocketClient {
    private static final ObjectMapper objectMapper = ObjectMapperProvider.getInstance();
    private final String clientId;
    private boolean matched = false;
    private boolean gameStarted = false;
    private int frameCount = 0;
    private final Random random = new Random();

    public TestClient(URI serverUri, String clientId) {
        super(serverUri);
        this.clientId = clientId;
    }

    @Override
    public void onOpen(ServerHandshake handshake) {
        System.out.println("[" + clientId + "] 连接建立成功");
        // 连接成功后发送匹配请求
        sendMatchRequest();
    }

    @Override
    public void onMessage(String message) {
        try {
            JsonNode jsonNode = objectMapper.readTree(message);
            String type = jsonNode.get("type").asText();

            switch (type) {
                case "matchSuccess":
                    handleMatchSuccess(jsonNode);
                    break;
                case "gameStart":
                    handleGameStart();
                    break;
                case "frameSync":
                    handleFrameSync(jsonNode);
                    break;
                default:
                    System.out.println("[" + clientId + "] 收到未知消息类型: " + type);
            }
        } catch (Exception e) {
            e.printStackTrace();
        }
    }

    @Override
    public void onClose(int code, String reason, boolean remote) {
        System.out.println("[" + clientId + "] 连接关闭: " + reason);
    }

    @Override
    public void onError(Exception ex) {
        ex.printStackTrace();
    }

    private void sendMatchRequest() {
        try {
            ObjectNode request = objectMapper.createObjectNode();
            request.put("type", "match");
            
            ObjectNode data = objectMapper.createObjectNode();
            data.put("name", clientId);
            
            request.set("data", data);
            
            String message = objectMapper.writeValueAsString(request);
            send(message);
            System.out.println("[" + clientId + "] 发送匹配请求: " + message);
        } catch (Exception e) {
            e.printStackTrace();
        }
    }

    private void handleMatchSuccess(JsonNode message) {
        matched = true;
        System.out.println("[" + clientId + "] 匹配成功.message:" +  message);
        // 打印房间ID和你的campId和token
        String roomId = message.get("roomId").asText();
        int campId = message.get("yourCampId").asInt();
        String token = message.get("yourToken").asText();
        JsonNode data = message.get("data");
        System.out.println("[" + clientId + "] 房间ID: " + roomId + ", CampID: " + campId + ", Token: " + token);

        // 发送准备就绪消息
        try {
            ObjectNode request = objectMapper.createObjectNode();
            request.put("type", "ready");
            String readyMessage = objectMapper.writeValueAsString(request);
            send(readyMessage);
            System.out.println("[" + clientId + "] 发送准备就绪");
        } catch (Exception e) {
            e.printStackTrace();
        }
    }

    private void handleGameStart() {
        gameStarted = true;
        System.out.println("[" + clientId + "] 游戏开始");
    }

    private void handleFrameSync(JsonNode message) {
        int frame = message.get("frame").asInt();
        System.out.println("[" + clientId + "] 收到帧同步数据 - 帧号: " + frame + ", 数据: " + message.toString());
        
        // 模拟发送下一帧的输入数据
//        sendFrameInput(frame + 2);
    }

    private void sendFrameInput(int frame) {
        try {
            ObjectNode request = objectMapper.createObjectNode();
            request.put("type", "frameInput");
            request.put("frame", frame);
            
            ArrayNode inputs = objectMapper.createArrayNode();
            ObjectNode input = objectMapper.createObjectNode();
            input.put("id", clientId);
            
            ArrayNode inputData = objectMapper.createArrayNode();
            // 添加一些随机输入数据
            for (int i = 0; i < 3; i++) {
                inputData.add(random.nextInt(10));
            }
            
            input.set("input", inputData);
            inputs.add(input);
            
            request.set("inputs", inputs);
            
            String message = objectMapper.writeValueAsString(request);
            send(message);
            System.out.println("[" + clientId + "] 发送第 " + frame + " 帧输入数据");
        } catch (Exception e) {
            e.printStackTrace();
        }
    }

    public static void main(String[] args) {
        try {
            if (args.length < 1) {
                System.out.println("请提供客户端ID作为参数，例如: Player1");
                return;
            }
            
            String clientId = args[0];
            
            // 启动客户端
            TestClient client = new TestClient(new URI("ws://localhost:8080/ws"), clientId);
            client.connect();
            
            // 等待用户输入退出指令
            Scanner scanner = new Scanner(System.in);
            System.out.println("按回车键退出...");
            scanner.nextLine();
            
            client.close();
        } catch (Exception e) {
            e.printStackTrace();
        }
    }
}