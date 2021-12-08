using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine.UI;
using System.Threading;
using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

public class Sender : MonoBehaviour
{
    WebCamTexture webCam;
    public RawImage myImage;
    public bool enableLog = false;
    Texture2D currentTexture;

    private TcpListener listener;
    private const int port = 8010;
    private bool stop = false;

    private List<TcpClient> clients = new List<TcpClient>();

    const int SEND_RECEIVE_COUNT = 15;
    // Start is called before the first frame update
    void Start()
    {
        Application.runInBackground = true;

        StartCoroutine(initAndWaitForWebCamTexture());
    }

    void byteLengthToFrameByteArray(int byteLength, byte[] fullBytes)
    {
        Array.Clear(fullBytes, 0, fullBytes.Length);
        byte[] bytesToSendCount = BitConverter.GetBytes(byteLength);
        bytesToSendCount.CopyTo(fullBytes, 0);
    }

    int frameByteArrayToByteLength(byte[] frameBytesLength)
    {
        int byteLength = BitConverter.ToInt32(frameBytesLength, 0);
        return byteLength;
    }

    IEnumerator initAndWaitForWebCamTexture()
    {
        webCam = new WebCamTexture();
        webCam.deviceName = WebCamTexture.devices[WebCamTexture.devices.Length - 1].name;

        webCam.requestedHeight = 10;
        webCam.requestedWidth = 10;

        myImage.texture = webCam;

        webCam.Play();

        currentTexture = new Texture2D(webCam.width, webCam.height);

        listener = new TcpListener(IPAddress.Any, port);

        listener.Start();

        while (webCam.width < 100)
        {
            yield return null;
        }

        StartCoroutine(senderCOR());
    }

    WaitForEndOfFrame endOfFrame = new WaitForEndOfFrame();

    IEnumerator senderCOR()
    {
        bool isConnected = false;
        TcpClient client = null;
        NetworkStream stream = null;

        Loom.RunAsync(() =>
        {
            while (!stop)
            {
                client = listener.AcceptTcpClient();
                clients.Add(client);

                isConnected = true;
                stream = client.GetStream();
            }
        });

        while (!isConnected)
        {
            yield return null;
        }

        LOG("Connected!");

        bool readyToGetFrame = true;

        byte[] frameBytesLength = new byte[SEND_RECEIVE_COUNT];

        while (!stop)
        {
            yield return endOfFrame;

            currentTexture.SetPixels(webCam.GetPixels());
            byte[] pngBytes = currentTexture.EncodeToPNG();

            byteLengthToFrameByteArray(pngBytes.Length, frameBytesLength);

            readyToGetFrame = false;

            Loom.RunAsync(() =>
            {
                stream.Write(frameBytesLength, 0, frameBytesLength.Length);
                LOG("Sent Image byte Length: " + frameBytesLength.Length);

                stream.Write(pngBytes, 0, pngBytes.Length);
                LOG("Sending Image byte array data: " + pngBytes.Length);

                readyToGetFrame = true;
            });

            while (!readyToGetFrame)
            {
                LOG("Waiting to get new frame");
                yield return null;
            }
        }
    }

    void LOG(string message)
    {
        if (enableLog)
            Debug.Log(message);
    }

    // Update is called once per frame
    void Update()
    {
        myImage.texture = webCam;
    }

    private void OnApplicationQuit()
    {
        if (webCam != null && webCam.isPlaying)
        {
            webCam.Stop();
            stop = true;
        }

        if (listener != null)
        {
            listener.Stop();
        }

        foreach (TcpClient c in clients)
            c.Close();
    }
}
