using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class KCPTest : MonoBehaviour
{
    private KcpProject.UDPSession connection = null;
    private bool stopSend = false;
    private byte[] buffer = new byte[1500];
    private int counter = 0;
    private int sendBytes = 0;
    private int recvBytes = 0;
    private float timeElapsed = 0f;
    private float updateInterval = 0.1f; // 100ms更新一次

    void Start()
    {
        try
        {
            connection = new KcpProject.UDPSession
            {
                AckNoDelay = true,
                WriteDelay = false
            };
            connection.Connect("127.0.0.1", 8888);
            Debug.Log("KCP Connected to 127.0.0.1:8888");
        }
        catch (Exception e)
        {
            Debug.LogError("KCP Connection failed: " + e.Message);
        }
    }

    void Update()
    {
        if (connection == null) return;

        // 定期更新KCP
        timeElapsed += Time.deltaTime;
        if (timeElapsed >= updateInterval)
        {
            connection.Update();
            timeElapsed = 0f;
        }

        if (!stopSend)
        {
            var sent = connection.Send(buffer, 0, buffer.Length);
            if (sent < 0)
            {
                Debug.LogError("Write message failed.");
                return;
            }

            if (sent > 0)
            {
                counter++;
                sendBytes += buffer.Length;
                if (counter >= 500)
                    stopSend = true;
                
                Debug.Log($"Sent message #{counter}");
            }
        }

        var n = connection.Recv(buffer, 0, buffer.Length);
        if (n == 0)
        {
            // 没有数据可接收，继续下一帧
            return;
        }
        else if (n < 0)
        {
            Debug.LogError("Receive Message failed.");
            return;
        }
        else
        {
            recvBytes += n;
            Debug.Log($"{recvBytes} / {sendBytes} bytes");
        }
    }

    private void OnDestroy()
    {
        if (connection != null)
        {
            connection.Close();
        }
    }
}