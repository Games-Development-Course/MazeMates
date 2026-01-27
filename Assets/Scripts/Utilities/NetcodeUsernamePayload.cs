using System.Text;
using Unity.Netcode;

public static class NetcodeUsernamePayload
{
    public static void ApplyFromAuthManager()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        string u = MazeMates.Authentication.UgsAuthManager.Instance != null
            ? MazeMates.Authentication.UgsAuthManager.Instance.CurrentUsername
            : null;

        u = (u ?? "").Trim();
        nm.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(u);
    }
}
