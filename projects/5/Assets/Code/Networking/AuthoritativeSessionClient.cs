using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace DunGen.Networking
{
    public sealed class AuthoritativeSessionClient : MonoBehaviour
    {
        [SerializeField] private BackendConnectionConfig connectionConfig;
        [SerializeField] private string sessionIdOverride = "";
        [SerializeField] private bool autoPollBootstrap;
        [SerializeField] private bool autoPollTimeline;
        [SerializeField] private bool autoPollWorld;
        [SerializeField] private int timelineTake = 200;

        public event Action<UnitySessionBootstrapDto> BootstrapUpdated;
        public event Action<UnitySessionTimelineDto> TimelineUpdated;
        public event Action<UnitySessionWorldDto> WorldUpdated;
        public event Action<PooledDungeonClaimDto> DungeonClaimed;
        public event Action<UnityMasteryOfferDto> MasteryOfferUpdated;
        public event Action<UnityMasterySelectionResultDto> MasterySelectionCompleted;
        public event Action<UnityMasteryProgressDto> MasteryProgressUpdated;
        public event Action<string> RequestFailed;

        private Coroutine _bootstrapPollingRoutine;
        private Coroutine _timelinePollingRoutine;
        private Coroutine _worldPollingRoutine;

        public string SessionId => string.IsNullOrWhiteSpace(sessionIdOverride)
            ? (connectionConfig != null ? connectionConfig.DefaultSessionId : "session-001")
            : sessionIdOverride.Trim();

        /// <summary>Inject config at runtime (used by <see cref="NetworkingBootstrap"/>).</summary>
        public void SetConfig(BackendConnectionConfig config) => connectionConfig = config;

        private void OnEnable()
        {
            if (connectionConfig == null)
                return;

            if (autoPollBootstrap)
                _bootstrapPollingRoutine = StartCoroutine(PollBootstrapLoop());

            if (autoPollTimeline)
                _timelinePollingRoutine = StartCoroutine(PollTimelineLoop());

            if (autoPollWorld)
                _worldPollingRoutine = StartCoroutine(PollWorldLoop());
        }

        private void OnDisable()
        {
            if (_bootstrapPollingRoutine != null)
            {
                StopCoroutine(_bootstrapPollingRoutine);
                _bootstrapPollingRoutine = null;
            }

            if (_timelinePollingRoutine != null)
            {
                StopCoroutine(_timelinePollingRoutine);
                _timelinePollingRoutine = null;
            }

            if (_worldPollingRoutine != null)
            {
                StopCoroutine(_worldPollingRoutine);
                _worldPollingRoutine = null;
            }
        }

        public void RefreshBootstrap()
        {
            if (connectionConfig == null)
            {
                RequestFailed?.Invoke("Missing BackendConnectionConfig reference.");
                return;
            }

            StartCoroutine(GetJson(connectionConfig.BuildClientBootstrapUrl(SessionId), json =>
            {
                var dto = JsonUtility.FromJson<UnitySessionBootstrapDto>(json);
                BootstrapUpdated?.Invoke(dto);
            }));
        }

        public void RefreshTimeline(int take = 200)
        {
            if (connectionConfig == null)
            {
                RequestFailed?.Invoke("Missing BackendConnectionConfig reference.");
                return;
            }

            StartCoroutine(GetJson(connectionConfig.BuildClientTimelineUrl(SessionId, take), json =>
            {
                var dto = JsonUtility.FromJson<UnitySessionTimelineDto>(json);
                TimelineUpdated?.Invoke(dto);
            }));
        }

        public void SetSessionIdOverride(string sessionId)
        {
            sessionIdOverride = sessionId ?? string.Empty;
        }

        public void RefreshWorld()
        {
            if (connectionConfig == null)
            {
                RequestFailed?.Invoke("Missing BackendConnectionConfig reference.");
                return;
            }

            StartCoroutine(GetJson(connectionConfig.BuildClientWorldUrl(SessionId), json =>
            {
                var dto = JsonUtility.FromJson<UnitySessionWorldDto>(json);
                WorldUpdated?.Invoke(dto);
            }));
        }

        public void ClaimDungeonFromPool(int difficultyLevel = 1, Action<PooledDungeonClaimDto> onSuccess = null, Action<string> onError = null)
        {
            if (connectionConfig == null)
            {
                var err = "Missing BackendConnectionConfig reference.";
                RequestFailed?.Invoke(err);
                onError?.Invoke(err);
                return;
            }

            var url = connectionConfig.BuildPoolClaimUrl(difficultyLevel);
            StartCoroutine(PostWithoutBody(url, json =>
            {
                var dto = JsonUtility.FromJson<PooledDungeonClaimDto>(json);
                if (dto != null && !string.IsNullOrEmpty(dto.executionId))
                {
                    DungeonClaimed?.Invoke(dto);
                    onSuccess?.Invoke(dto);
                }
                else
                {
                    var msg = "Pool claim returned invalid or empty dungeon payload.";
                    RequestFailed?.Invoke(msg);
                    onError?.Invoke(msg);
                }
            }));
        }

        public void FetchBinarySnapshot(Action<byte[]> onSuccess, Action<string> onError = null)
        {
            if (connectionConfig == null)
            {
                var err = "Missing BackendConnectionConfig reference.";
                RequestFailed?.Invoke(err);
                onError?.Invoke(err);
                return;
            }

            var url = connectionConfig.BuildBinarySnapshotUrl(SessionId);
            StartCoroutine(GetRawBytes(url, onSuccess, onError));
        }

        private IEnumerator GetRawBytes(string url, Action<byte[]> onSuccess, Action<string> onError)
        {
            using (var request = UnityWebRequest.Get(url))
            {
                if (connectionConfig != null)
                {
                    request.timeout = connectionConfig.RequestTimeoutSeconds;
                    ClientInteractionSecurityLayer.ApplySecurityHeaders(request, connectionConfig);
                }

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    var err = $"GET (binary) {url} failed: {request.error}";
                    RequestFailed?.Invoke(err);
                    onError?.Invoke(err);
                    yield break;
                }

                onSuccess?.Invoke(request.downloadHandler.data);
            }
        }

        public void RequestMasteryOffer(string userId, string itemType, string masteryTier)
        {
            if (connectionConfig == null)
            {
                RequestFailed?.Invoke("Missing BackendConnectionConfig reference.");
                return;
            }

            var url = connectionConfig.BuildMasteryOfferUrl(userId, itemType, masteryTier);
            StartCoroutine(PostWithoutBody(url, json =>
            {
                var dto = JsonUtility.FromJson<UnityMasteryOfferDto>(json);
                MasteryOfferUpdated?.Invoke(dto);
            }));
        }

        public void SelectMasteryOption(string userId, string offerId, string skillId)
        {
            if (connectionConfig == null)
            {
                RequestFailed?.Invoke("Missing BackendConnectionConfig reference.");
                return;
            }

            var url = connectionConfig.BuildMasterySelectUrl(userId, offerId, skillId);
            StartCoroutine(PostWithoutBody(url, json =>
            {
                var dto = JsonUtility.FromJson<UnityMasterySelectionResultDto>(json);
                MasterySelectionCompleted?.Invoke(dto);
            }));
        }

        public void RefreshMasteryProgress(string userId, string itemType)
        {
            if (connectionConfig == null)
            {
                RequestFailed?.Invoke("Missing BackendConnectionConfig reference.");
                return;
            }

            var url = connectionConfig.BuildMasteryProgressUrl(userId, itemType);
            StartCoroutine(GetJson(url, json =>
            {
                var dto = JsonUtility.FromJson<UnityMasteryProgressDto>(json);
                MasteryProgressUpdated?.Invoke(dto);
            }));
        }

        private IEnumerator PollBootstrapLoop()
        {
            while (true)
            {
                RefreshBootstrap();
                yield return new WaitForSeconds(connectionConfig.PollIntervalSeconds);
            }
        }

        private IEnumerator PollTimelineLoop()
        {
            while (true)
            {
                RefreshTimeline(timelineTake);
                yield return new WaitForSeconds(connectionConfig.PollIntervalSeconds);
            }
        }

        private IEnumerator PollWorldLoop()
        {
            while (true)
            {
                RefreshWorld();
                yield return new WaitForSeconds(connectionConfig.PollIntervalSeconds);
            }
        }

        private IEnumerator GetJson(string url, Action<string> onSuccess)
        {
            using (var request = UnityWebRequest.Get(url))
            {
                if (connectionConfig != null)
                {
                    request.timeout = connectionConfig.RequestTimeoutSeconds;
                    ClientInteractionSecurityLayer.ApplySecurityHeaders(request, connectionConfig);
                }

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    RequestFailed?.Invoke($"GET {url} failed: {request.error}");
                    yield break;
                }

                onSuccess?.Invoke(request.downloadHandler.text);
            }
        }

        private IEnumerator PostWithoutBody(string url, Action<string> onSuccess)
        {
            using (var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(Array.Empty<byte>());
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                if (connectionConfig != null)
                {
                    request.timeout = connectionConfig.RequestTimeoutSeconds;
                    ClientInteractionSecurityLayer.ApplySecurityHeaders(request, connectionConfig);
                }

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    RequestFailed?.Invoke($"POST {url} failed: {request.error}");
                    yield break;
                }

                onSuccess?.Invoke(request.downloadHandler.text);
            }
        }
    }

    [Serializable]
    public sealed class PooledDungeonClaimDto
    {
        public string poolId;
        public string executionId;
        public int difficultyLevel;
        public int seed;
        public int width;
        public int height;
        public int roomCount;
        public int enemyCount;
        public int lootCount;
        public string claimedAt;
    }

    [Serializable]
    public sealed class UnitySessionBootstrapDto
    {
        public string sessionId;
        public bool hasWorld;
        public string executionId;
        public int roomCount;
        public int enemyCount;
        public int lootCount;
        public string snapshotUrl;
        public string streamUrl;
        public string webSocketUrl;
        public string timelineUrl;
    }

    [Serializable]
    public sealed class UnitySessionTimelineDto
    {
        public string sessionId;
        public WorldSessionEventDto[] events;
    }

    [Serializable]
    public sealed class UnitySessionWorldDto
    {
        public string sessionId;
        public string executionId;
        public UnityGeneratedWorldDto world;
    }

    [Serializable]
    public sealed class UnityGeneratedWorldDto
    {
        public int seed;
        public int width;
        public int height;
        public int dungeonLevel;
        public UnityWorldRoomDto[] rooms = Array.Empty<UnityWorldRoomDto>();
        public UnityWorldEnemyDto[] enemies = Array.Empty<UnityWorldEnemyDto>();
        public UnityWorldLootDto[] loot = Array.Empty<UnityWorldLootDto>();
        public UnityTerrainMeshDto terrainMesh;
    }

    [Serializable]
    public sealed class UnityTerrainMeshDto
    {
        public string meshId;
        public int width;
        public int height;
        public int seed;
        public string algorithm;
        public float waterLevel;
        public float heightScale;
        public float minHeight;
        public float maxHeight;
        public UnityTerrainMeshVertexDto[] vertices = Array.Empty<UnityTerrainMeshVertexDto>();
        public int[] triangles = Array.Empty<int>();
    }

    [Serializable]
    public sealed class UnityTerrainMeshVertexDto
    {
        public float x;
        public float y;
        public float z;
        public float u;
        public float v;
        public float normalX;
        public float normalY;
        public float normalZ;
    }

    [Serializable]
    public sealed class UnityWorldRoomDto
    {
        public int id;
        public int x;
        public int y;
        public int width;
        public int height;
    }

    [Serializable]
    public sealed class UnityWorldEnemyDto
    {
        public int id;
        public string archetype;
        public int x;
        public int y;
        public int level;
    }

    [Serializable]
    public sealed class UnityWorldLootDto
    {
        public string itemId;
        public string itemType;
        public string tier;
        public int x;
        public int y;
    }

    [Serializable]
    public sealed class WorldSessionEventDto
    {
        public string eventId;
        public string sessionId;
        public string eventType;
        public string category;
        public uint frame;
        public string entityId;
        public string message;
        public string timestampUtc;
    }

    [Serializable]
    public sealed class UnityMasteryOptionDto
    {
        public string skillId;
        public string name;
        public string description;
        public string itemType;
        public string masteryTier;
        public int effectKind;
        public int power;
    }

    [Serializable]
    public sealed class UnityMasteryOfferDto
    {
        public string offerId;
        public string userId;
        public string itemType;
        public string masteryTier;
        public string createdAtUtc;
        public UnityMasteryOptionDto[] options = Array.Empty<UnityMasteryOptionDto>();
    }

    [Serializable]
    public sealed class UnityMasterySelectionResultDto
    {
        public string offerId;
        public string userId;
        public string itemType;
        public string masteryTier;
        public UnityMasteryOptionDto selectedOption;
        public UnityMasteryOptionDto[] unlockedForItemType = Array.Empty<UnityMasteryOptionDto>();
    }

    [Serializable]
    public sealed class UnityMasteryProgressDto
    {
        public string userId;
        public string itemType;
        public int unlockedCount;
        public UnityMasteryOptionDto[] unlockedOptions = Array.Empty<UnityMasteryOptionDto>();
    }
}
