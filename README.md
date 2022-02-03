# Unity Multiplayer Playground
Various demos while teaching Netcode For GameObjects features in [YouTube](https://www.youtube.com/dilmerv)

NOTE!!! - This project is forked from DilmerV's fantastic youtube multiplayer series
on Netcode for Gameobjects.

The code in this (forked Repo) contains several modifications on top of Dilmer's original code, mostly cosmetic, but some functional.

The primary changes found here:

1. When host creates a new game, or clients connect - they are provided with a random body color (instead of all the clients looking the same)
2. In the UIManager and PlayersManager scripts - the process of keeping track of connected Player count has been changed so that it follows more of an **Observer** pattern, instead of having the UIManager update the PlayerCount text EVERY frame.
3. Moved the UnityTransport component to its own prefab instead of having it on the NetworkManager directly - mostly just to mirror how Unity's BossGame demo has it.
4. There are several functional updates to the UIManager component to provide for more comprehensive 'Disconnect' process. i.e. if the Host disconnects, a ClientRpc is sent via the PlayersManager script to ask all connected clients to disconnect first. 
5. A new DefaultVCam (Virtual Camera) has been placed in some scenes where the Followcam was used. When a disconnect occurs, the DefaultVCam is given priority so the user isn't left 'spinning' due to a Followcam client that is no longer connected.
6. The Canvas UI has been modified to include a new Disconnect button, and cleaner UI for Debug area, so elements don't overlap if the screen is resized. Buttons that aren't needed (i.e. Execute Physics) are hidden if that Client isn't the host, etc. (mostly cosmetic)
