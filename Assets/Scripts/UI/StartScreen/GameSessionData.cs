public static class GameSessionData
{
    public static string HostName;
    public static string ClientName;
    public static GameMode SelectedMode;
}

public enum GameMode
{
    Easy,
    Medium,
    Hard,
    Tutorial
}
