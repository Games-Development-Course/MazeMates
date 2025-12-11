using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class FusionUIManager : MonoBehaviour
{
    public NetworkRunner runner;
    public Button hostButton;
    public Button joinButton;
    public TMP_InputField codeField;

    private void Start()
    {
        hostButton.onClick.AddListener(StartHost);
        joinButton.onClick.AddListener(StartClient);
    }

    private async void StartHost()
    {
        if (runner == null) runner = FindObjectOfType<NetworkRunner>();

        var args = new StartGameArgs()
        {
            GameMode = GameMode.Host,
            SessionName = "MAZEMATES_" + Random.Range(1000, 9999),
            Scene = SceneRef.FromIndex(0),
            SceneManager = runner.GetComponent<NetworkSceneManagerDefault>()
        };

        await runner.StartGame(args);
        codeField.text = args.SessionName;

        Debug.Log("HOST started");
    }

    private async void StartClient()
    {
        if (runner == null) runner = FindObjectOfType<NetworkRunner>();

        string room = codeField.text;

        if (string.IsNullOrWhiteSpace(room))
        {
            Debug.LogWarning("No code entered!");
            return;
        }

        var args = new StartGameArgs()
        {
            GameMode = GameMode.Client,
            SessionName = room,
            Scene = SceneRef.None,
            SceneManager = runner.GetComponent<NetworkSceneManagerDefault>()

        };

        await runner.StartGame(args);
        Debug.Log("CLIENT joined room: " + room);
    }
}
