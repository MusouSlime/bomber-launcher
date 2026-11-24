# Networking scope and test results

Status: first online pass implemented and partially tested. PC 2.2, Steam
lobbies/P2P, single-client test on 2026-11-24.

## How online actually works

SBR1 on PC does **not** depend on a live gameplay server. The active path is:

- **Steam lobbies** for rooms and matchmaking
- **Steam P2P** for battle packets
- Legacy FUSE/NGS types still exist in the binary, but the PC path is
  Steamworks

So the game is not “dead because the servers shut down.” The common problems
are empty public rooms, NAT/CGNAT connectivity, invites and lobby discovery.

## Confirmed issues

### 1. Steam P2P relay was never enabled

`Steamworks.SteamNetworking.AllowP2PPacketRelay` exists in the assembly but the
game never calls it. Players behind strict NAT, CGNAT or restrictive firewalls
can fail to connect even after joining a lobby.

**Implemented:** call `AllowP2PPacketRelay(true)` after Steamworks starts.

### 2. Lobby search is distance-limited

`requestLobbyListAsync` applies `AddRequestLobbyListDistanceFilter` from the
game’s random-match setting. A “nearby” default can hide joinable rooms.

**Implemented:** force `Worldwide` by default, configurable.

### 3. Full rooms are not filtered out

The game never calls `AddRequestLobbyListFilterSlotsAvailable` or
`AddRequestLobbyListResultCountFilter`. The list can contain full/unjoinable
rooms and uses Steam’s default result count.

**Implemented:** request only rooms with at least one slot and raise the result
count to 100.

### 4. Steam invites are ignored

`NetworkManager/SteamworksNetwork.onLobbyInvite` is empty, and
`GameLobbyJoinRequested_t` is never registered. Steam “Join Game” invites do
nothing.

**Not implemented yet.** Needs a callback that joins the lobby and enters the
game’s online ready flow.

### 5. Matchmaking has no visible timeout/cancel path

Quick Play enters **“Finding Room…”** and stays there. The B button cancels, but
there is no timeout or “no rooms found” return.

**Not implemented yet.** A plugin-side timeout or a room browser would improve
this.

### 6. Host migration/reconnect code already exists

`NetworkManager` has `_roomMigrating`, disconnect states and retry waits. Test
these before rewriting anything.

## What was added

Config section:

```ini
[Network]
EnableRelay = true
ForceWorldwide = true
FilterFullLobbies = true
LobbyResultCount = 100
LogSearch = true
```

Behavior:

- Calls `SteamNetworking.AllowP2PPacketRelay(true)` after startup.
- Forces `ELobbyDistanceFilterWorldwide` when a lobby search applies its
  distance filter.
- Adds `AddRequestLobbyListFilterSlotsAvailable(1)` and
  `AddRequestLobbyListResultCountFilter(100)` before `RequestLobbyList`.
- Logs the applied filters and the relay result.

The first distance patch attempt failed because Harmony matched the parameter
name `distanceFilter`; the game uses `eLobbyDistanceFilter`. That is fixed.

## Test results

### Baseline

- Battle → Online Battle is reachable.
- Terms of Use prompt works.
- Free Battle → Quick Play enters “Finding Room…”.
- There were no public rooms; the spinner did not time out.
- B cancels the search and returns to the menu.

### With the patch

Observed in `BepInEx/LogOutput.log`:

```text
Steam P2P relay attempt 1: enabled
Steam lobby search: worldwide distance
Steam lobby search: slots>=1, max=100
```

The search still found no rooms, which is expected with the current public
playerbase. This confirms the patched code path runs, but not that another
player can join yet.

### Not yet tested

- Creating a room and joining it from a second Steam client.
- Search by room number.
- Steam friend invite / “Join Game”.
- Relay on vs off behind strict NAT.
- 4–8 player sync and host migration.
- Full-room filtering with a real full room.
- Lobby result count and “no rooms” behavior.

## Create Room UI

The Create Room screen is reachable:

```text
Rules
Player
Room type
Sets
Time
Start Position
Revenge Carts
Pressure Blocks
```

Room settings can be changed. The final create action was not completed in the
automated test; the UI state/confirm path needs a manual pass. The underlying
`NetworkManager.createStdInetRoom(RoomGameInfo, RandomMatchSettings)` and
`createLobbyAsync` paths exist and are used by the scene.

## Recommended next steps

1. Test with **two Steam clients**:
   - create a room on client A;
   - join by room number and by friend invite on client B;
   - verify relay on/off from a strict NAT or VPN.
2. Add a **lobby search result count** log and a “no rooms found” exit so Quick
   Play cannot spin forever.
3. Implement the **Steam invite / Join Game** callback.
4. Add a small **room browser** overlay (host, players, map, rule, ping) if the
   native UI remains awkward.
5. Only then consider custom maps/characters online; require all clients to
   have the same mod (lobby data hash) or block custom content online.

## Not realistic

- Reviving “official servers” — there is no gameplay server to revive.
- Dedicated servers — needs the P2P protocol reverse-engineered.
- Rollback or new netcode — the game is lockstep/input-delay; major rewrite.
- Cross-play with Switch/consoles — different platform backends and assets.
- Creating a playerbase — discovery can improve, but code cannot fill rooms.
