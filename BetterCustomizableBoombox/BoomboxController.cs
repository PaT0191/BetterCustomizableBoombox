using GameNetcodeStuff;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using YoutubeBoombox.Providers;
using static BetterYoutubeBoombox.YoutubeBoomboxConfig;
using static BetterYoutubeBoombox.YoutubeBoomboxPlugin;

namespace BetterYoutubeBoombox
{
    public class BoomboxController : NetworkBehaviour
    {
        public static BoomboxController Instance { get; set; }

        public BoomboxItem Boombox { get; set; }

        private YoutubeBoomboxGUI GUI { get; set; }

        private ParsedUri CurrentUri { get; set; }

        private string CurrentId { get; set; }

        private string CurrentUrl { get; set; }

        private bool IsPlaylist { get; set; }

        private int PlaylistCurrentIndex { get; set; } = 0;

        private List<ulong> ReadyClients { get; set; } = new List<ulong>();

        private NetworkList<ulong> ClientsNeededToBeReady { get; } = new NetworkList<ulong>();

        private bool isBoomboxActive { get; set; }

        public void Awake()
        {
            Instance = this;
            Boombox = GetComponent<BoomboxItem>();
            Boombox.musicAudios = null;
        }

        public void Start()
        {
            DebugLog($"Boombox started client: {IsClient} host: {IsHost} server: {IsServer}");
            ClientsNeededToBeReady.Initialize(this);
            IHaveTheModServerRpc();
        }

        public void OpenMenu()
        {
            if (StartOfRound.Instance != null && StartOfRound.Instance.localPlayerController.currentlyHeldObjectServer == Boombox)
            {
                DebugLog($"Boombox button pressed!");

                GUI = gameObject.AddComponent<YoutubeBoomboxGUI>();

                DisableControls();
            }
            else
                DebugLog($"Boombox button cancelled!");
        }

        public void ToggleBoombox(bool startMusic, bool pitchDown)
        {
            DebugLog($"Start Music {startMusic}");
            isBoomboxActive = startMusic;
            if (startMusic)
            {
                IncrementPlaylistIndex();
                return;
            }
            DebugLog($"StopMusicServerRpc");

            StopMusicServerRpc(startMusic, pitchDown);

        }

        private void DisableControls()
        {
            StartOfRound.Instance.localPlayerController.isTypingChat = true;
            StartOfRound.Instance.localPlayerController.playerActions.Disable();
        }

        private void EnableControls()
        {
            StartOfRound.Instance.localPlayerController.isTypingChat = false;
            StartOfRound.Instance.localPlayerController.playerActions.Enable();
        }

        [ServerRpc(RequireOwnership = false)]
        public void IHaveTheModServerRpc(ServerRpcParams serverRpcParams = default)
        {
            DebugLog($"Regsitering mod server rpc called");

            if (!IsServer)
            {
                return;
            }
            // Tells which client is calling this method
            ulong sender = serverRpcParams.Receive.SenderClientId;
            DebugLog($"Client needed to be ready {ClientsNeededToBeReady}");

            if (!ClientsNeededToBeReady.Contains(sender))
            {
                DebugLog($"{sender} has registered having this mod");

                ClientsNeededToBeReady.Add(sender);
            }
        }

        public void ClientDisconnected(ulong clientId)
        {
            if (!IsServer) return;

            ClientsNeededToBeReady.Remove(clientId);
        }

        [ServerRpc(RequireOwnership = false)]
        public void DownloadServerRpc(string originalUrl, string id, string downloadUrl, UriType uriType)
        {
            DebugLog($"Download server rpc received, sending to all");
            DownloadClientRpc(originalUrl, id, downloadUrl, uriType);
        }

        [ClientRpc]
        public void DownloadClientRpc(string originalUrl, string id, string downloadUrl, UriType uriType)
        {
            DebugLog($"Download request received on client, processing.");
            ProcessRequest(new ParsedUri(new Uri(originalUrl), id, downloadUrl, uriType));
        }

        public void Download(ParsedUri parsedUri)
        {
            DebugLog($"Download called, calling everywhere");
            DownloadServerRpc(parsedUri.Uri.OriginalString, parsedUri.Id, parsedUri.DownloadUrl, parsedUri.UriType);
        }

        [ServerRpc(RequireOwnership = false)]
        public void IAmReadyServerRpc(ServerRpcParams serverRpcParams = default)
        {
            if (!IsServer) return;

            ulong sender = serverRpcParams.Receive.SenderClientId;

            DebugLog($"Ready called from {sender}");

            AddReadyClientRpc(sender);
        }

        [ClientRpc]
        public void AddReadyClientRpc(ulong readyId)
        {
            DebugLog($"READY CLIENT CALLED already ready?: {ReadyClients.Contains(readyId)}");
            if (ReadyClients.Contains(readyId)) return;

            ReadyClients.Add(readyId);

            DebugLog($"READY CLIENT {ReadyClients.Count}/{ClientsNeededToBeReady.Count}");

            if (ReadyClients.Count >= ClientsNeededToBeReady.Count)
            {
                DebugLog($"Everyone ready, starting tunes!");
                ReadyClients.Clear();
                Boombox.boomboxAudio.loop = true;
                Boombox.boomboxAudio.pitch = 1;
                Boombox.isBeingUsed = true;
                Boombox.isPlayingMusic = true;
                Boombox.boomboxAudio.Play();

                if (IsPlaylist)
                {
                    DebugLog($"Currently playing playlist, starting playlist routine.");
                    Boombox.boomboxAudio.loop = false;
                    Boombox.StartCoroutine(PlaylistCoroutine());
                }
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void StopMusicServerRpc(bool startMusic, bool pitchDown)
        {
            if(!startMusic)
                StopMusicClientRpc(pitchDown);
        }

        [ClientRpc]
        public void StopMusicClientRpc(bool pitchDown)
        {
            if (pitchDown)
            {
                StartCoroutine(musicPitchDown());
            }
            else
            {
                Boombox.boomboxAudio.Stop();
                Boombox.boomboxAudio.PlayOneShot(Boombox.stopAudios[UnityEngine.Random.Range(0, Boombox.stopAudios.Length)]);
            }


            Boombox.isBeingUsed = false;
            Boombox.isPlayingMusic = false;
            ResetReadyClients();
        }

        private IEnumerator musicPitchDown()
        {
            for (int i = 0; i < 30; i++)
            {
                yield return null;
                Boombox.boomboxAudio.pitch -= 0.033f;
                if (Boombox.boomboxAudio.pitch <= 0f)
                {
                    break;
                }
            }
            Boombox.boomboxAudio.Stop();
            Boombox.boomboxAudio.PlayOneShot(Boombox.stopAudios[UnityEngine.Random.Range(0, Boombox.stopAudios.Length)]);
        }

        public void ResetReadyClients()
        {
            ReadyClients.Clear();
        }

        public void PlaySong(string url)
        {
            DebugLog($"Trying to play {url}");

            DebugLog("Boombox found");

            Uri uri = new Uri(url);

            ParsedUri parsedUri = Providers.First(p => p.Hosts.Contains(uri.Host)).ParseUri(uri);

            Download(parsedUri);
        }

        // Doesn't really destroy the GUI, the GUI destroys itself, just gotta set it to null.
        public void DestroyGUI()
        {
            GUI = null;

            EnableControls();
        }

        public bool IsGUIShowing()
        {
            return GUI != null;
        }

        public IEnumerator LoadSongCoroutine(string path)
        {
            DebugLog($"Loading song at {path}.");

            if (PathsThisSession.Contains(path)) PathsThisSession.Remove(path);

            PathsThisSession.Insert(0, path);

            if (PathsThisSession.Count > MaxCachedDownloads.Value)
            {
                File.Delete(PathsThisSession[PathsThisSession.Count - 1]);
                PathsThisSession.RemoveAt(PathsThisSession.Count - 1);
            }

            string url = string.Format("file://{0}", path);
            WWW www = new WWW(url);
            yield return www;

            DebugLog($"Successfully loaded song at {path}.");

            Boombox.boomboxAudio.clip = www.GetAudioClip(false, false);

            DebugLog("BOOMBOX READY!");

            IAmReadyServerRpc();
        }

        [ClientRpc]
        public void IncrementPlaylistIndex()
        {
            if (CurrentId == null)
            {
                DebugLog($"Playlist id is null");
                return;
            }

            DebugLog($"Incrementing playlist index.");
            Boombox.boomboxAudio.Stop();

            PlaylistCurrentIndex++;

            ReadyClients.Clear();

            if (InfoCache.PlaylistCache.TryGetValue(CurrentId, out List<string> videoIds))
            {
                if (PlaylistCurrentIndex < videoIds.Count)
                {
                    string id = videoIds[PlaylistCurrentIndex];
                    string url = $"https://youtube.com/watch?v={id}";

                    DebugLog($"Downloading next playlist song.");

                    DownloadSong(id, url);
                }
                else
                {
                    DebugLog($"Playlist complete!");
                    var randomSong = videoIds[UnityEngine.Random.Range(0, videoIds.Count)];
                    string newPath = Path.Combine(DownloadsPath, $"{randomSong}.mp3");
                    Boombox.StartCoroutine(LoadSongCoroutine(newPath));
                }
            } 
            else
            {
                DebugLog($"Playlist video ids not found! Cannot proceed!");
                IAmReadyServerRpc();
            }
        }

        public IEnumerator PlaylistCoroutine()
        {
            PrepareNextSongInPlaylist();
            while (Boombox.boomboxAudio.isPlaying)
            {
                yield return new WaitForSeconds(1);
            }

            if (!isBoomboxActive)
            {
                yield break;
            }
            IncrementPlaylistIndex();
        }

        public void DownloadCurrentVideo()
        {
            DebugLog($"Downloading {CurrentUrl} ({CurrentId})");
            DownloadSong(CurrentId, CurrentUrl);
        }

        public async void DownloadSong(string id, string url)
        {
            DebugLog($"Downloading song {url} ({id})");

            string newPath = Path.Combine(DownloadsPath, $"{id}.mp3");

            if (id == null || url == null || newPath == null)
            {
                DebugLog($"Something is null. {id == null} {url == null} {newPath == null}");

                return;
            }

            if (File.Exists(newPath))
            {
                DebugLog($"File exists. Reusing.");
                Boombox.StartCoroutine(LoadSongCoroutine(newPath));

                return;
            }

            if (InfoCache.DurationCache.TryGetValue(id, out float duration))
            {
                if (duration > MaxSongDuration.Value)
                {
                    DebugLog($"Song too long. Preventing download.");
                    IAmReadyServerRpc();

                    return;
                }
            }
            else
            {
                try
                {
                    DebugLog($"Downloading song duration data.");
                    var videoDataResult = await YoutubeBoomboxPlugin.Instance.YoutubeDL.RunVideoDataFetch(url);
                    DebugLog($"Downloaded song duration data.");

                    if (videoDataResult.Success && videoDataResult.Data.Duration != null)
                    {
                        InfoCache.DurationCache.Add(id, (float)videoDataResult.Data.Duration);
                        // Skip downloading videos that are too long
                        if (videoDataResult.Data.Duration > MaxSongDuration.Value)
                        {
                            DebugLog($"Song too long. Preventing download.");
                            IAmReadyServerRpc();

                            return;
                        }
                    }
                    else
                    {
                        DebugLog($"Couldn't get song data, skipping.");
                        IAmReadyServerRpc();

                        return;
                    }
                } 
                catch(Exception e)
                {
                    DebugLog($"Error while downloading song data.");
                    DebugLog(e);
                    IAmReadyServerRpc();

                    return;
                }
            }

            DebugLog($"Trying to download {url}.");

            var res = await YoutubeBoomboxPlugin.Instance.YoutubeDL.RunAudioDownload(url, YoutubeDLSharp.Options.AudioConversionFormat.Mp3);

            DebugLog($"Downloaded.");

            if (res.Success)
            {
                File.Move(res.Data, newPath);

                DebugLog($"Song {id} downloaded successfully.");
                Boombox.StartCoroutine(LoadSongCoroutine(newPath));
            }
            else
            {
                DebugLog($"Failed to download song {id}.");
                IAmReadyServerRpc();
            }
        }

        public void PrepareNextSongInPlaylist()
        {
            DebugLog($"Preparing next song in playlist");
            if (InfoCache.PlaylistCache.TryGetValue(CurrentId, out List<string> videoIds))
            {
                if (PlaylistCurrentIndex + 1 < videoIds.Count)
                {
                    string id = videoIds[PlaylistCurrentIndex + 1];
                    string url = $"https://youtube.com/watch?v={id}";

                    DebugLog($"Preparing {url} ({id})");

                    PrepareSong(id, url);
                }
                else
                {
                    DebugLog($"Playlist complete.");
                }
            }
            else
            {
                DebugLog($"Couldn't find playlist ids!");
            }
        }

        public async void PrepareSong(string id, string url)
        {
            DebugLog($"Preparing next song {id}");

            string newPath = Path.Combine(DownloadsPath, $"{id}.mp3");

            if (File.Exists(newPath))
            {
                DebugLog($"Already exists, reusing.");
                return;
            }

            if (InfoCache.DurationCache.TryGetValue(id, out float duration))
            {
                if (duration > MaxSongDuration.Value)
                {
                    DebugLog($"Song too long. Preventing download.");
                    return;
                }
            }
            else
            {
                var videoDataResult = await YoutubeBoomboxPlugin.Instance.YoutubeDL.RunVideoDataFetch(url);

                if (videoDataResult.Success && videoDataResult.Data.Duration != null)
                {
                    InfoCache.DurationCache.Add(id, (float)videoDataResult.Data.Duration);
                    // Skip preparing videos that are too long
                    if (videoDataResult.Data.Duration > MaxSongDuration.Value)
                    {
                        DebugLog($"Song too long. Preventing download.");
                        return;
                    }
                }
                else
                {
                    DebugLog($"Couldn't get song length. Skipping.");
                    return;
                }
            }

            var res = await YoutubeBoomboxPlugin.Instance.YoutubeDL.RunAudioDownload(url, YoutubeDLSharp.Options.AudioConversionFormat.Mp3);

            if (res.Success)
            {
                File.Move(res.Data, newPath);

                DebugLog($"Prepared {id} successfully");
            }
            else
            {
                DebugLog($"Downloading {id} failed!");
            }
        }

        public async void DownloadCurrentPlaylist()
        {
            DebugLog($"Downloading playlist from {CurrentUrl} ({CurrentId})");

            PlaylistCurrentIndex = 0;
            if (!InfoCache.PlaylistCache.TryGetValue(CurrentId, out List<string> videoIds))
            {
                DebugLog($"Playlist not found in cache, downloading all ids.");

                var playlistResult = await YoutubeBoomboxPlugin.Instance.YoutubeDL.RunVideoPlaylistDownload(CurrentUrl, 1, null, null, "bestvideo+bestaudio/best",
                    YoutubeDLSharp.Options.VideoRecodeFormat.None, default, null, new InfoCache(CurrentId),
                    new YoutubeDLSharp.Options.OptionSet()
                    {
                        FlatPlaylist = true,
                        DumpJson = true
                    });

                if (!playlistResult.Success)
                {
                    DebugLog($"Failed to download playlist ids. Unable to proceed.");
                    IAmReadyServerRpc();

                    return;
                }
                else
                {
                    videoIds = InfoCache.PlaylistCache[CurrentId];
                }
            }

            if (videoIds.Count == 0)
            {
                DebugLog($"Playlist video ids empty...");
                IAmReadyServerRpc();

                return;
            }

            string id = videoIds[0];
            string url = $"https://youtube.com/watch?v={id}";

            DebugLog($"First playlist song found: {url} ({id})... Downloading.");

            DownloadSong(id, url);
        }

        public void ProcessRequest(ParsedUri parsedUri)
        {
            string url = parsedUri.DownloadUrl;

            CurrentUri = parsedUri;
            CurrentUrl = url;
            IsPlaylist = parsedUri.UriType == UriType.Playlist;
            CurrentId = parsedUri.Id;

            DebugLog($"Processing request for {CurrentId} isPlaylist?: {IsPlaylist}");

            if (!IsPlaylist)
            {
                DownloadCurrentVideo();
            }
            else
            {
                DownloadCurrentPlaylist();
            }
        }
    }

    [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.OnClientDisconnect))]
    internal static class Left
    {
        private static void Prefix(StartOfRound __instance, ulong clientId)
        {
            if (!__instance.ClientPlayerList.ContainsKey(clientId)) return;

            foreach (BoomboxController controller in UnityEngine.Object.FindObjectsOfType<BoomboxController>())
            {
                controller.ClientDisconnected(clientId);
            }
        }
    }
}
