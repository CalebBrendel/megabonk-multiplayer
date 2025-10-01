using MelonLoader;
[assembly: MelonInfo(typeof(Megabonk.Multiplayer.MegabonkMultiplayer), "Megabonk Multiplayer", "0.2.0", "CalebB")]
[assembly: MelonGame(null, "Megabonk")]


namespace Megabonk.Multiplayer
{
public class MegabonkMultiplayer : MelonMod
{
private static bool _steamOk;
private Harmony _harmony;


public static bool IsHost => SteamLobby.IsHost;


public override void OnInitializeMelon()
{
MelonLogger.Msg("Megabonk Multiplayer init...");
_harmony = new Harmony("cb.megabonk.multiplayer");
_harmony.PatchAll();


InitSteam();
SteamLobby.OnLobbyEntered += LobbyEntered;
SteamLobby.OnLobbyLeft += LobbyLeft;
SteamLobby.Init();
}


private void InitSteam()
{
if (!Packsize.Test() || !DllCheck.Test())
{
MelonLogger.Error("Steamworks.NET Packsize/DllCheck failed.");
return;
}
_steamOk = SteamAPI.Init();
if (!_steamOk)
{
MelonLogger.Error("SteamAPI.Init failed.");
return;
}
}


public override void OnUpdate()
{
if (_steamOk) SteamAPI.RunCallbacks();


if (Input.GetKeyDown(KeyCode.F9)) SteamLobby.HostLobby();
if (Input.GetKeyDown(KeyCode.F10)) SteamLobby.ShowInviteOverlay();
if (Input.GetKeyDown(KeyCode.F11)) SteamLobby.LeaveLobby();
if (Input.GetKeyDown(KeyCode.F7))
{
if (IsHost) NetHost.Instance?.ToggleReady();
else NetClient.Instance?.ToggleReady();
}


if (IsHost) NetHost.Instance?.Tick();
else NetClient.Instance?.Tick();
}


public override void OnDeinitializeMelon()
{
NetHost.Instance?.Shutdown();
NetClient.Instance?.Shutdown();
SteamLobby.Shutdown();
if (_steamOk) SteamAPI.Shutdown();
_harmony?.UnpatchAll("cb.megabonk.multiplayer");
}


private void LobbyEntered(bool isHost, CSteamID lobby, CSteamID hostId)
{
MelonLogger.Msg($"Lobby entered: {lobby}, host={hostId}");
if (isHost) NetHost.StartListening();
else NetClient.ConnectToHost(hostId);
}


private void LobbyLeft()
{
MelonLogger.Msg("Lobby left");
NetHost.Instance?.Shutdown();
NetClient.Instance?.Shutdown();
}
}
}
