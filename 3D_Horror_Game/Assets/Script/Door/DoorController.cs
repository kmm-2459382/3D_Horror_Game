using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI; // ─── 【追加】NavMesh関係の機能を使用するため ───

public class DoorController : MonoBehaviour
{
    // --- 最深部のリスト構造（連動するドアグループの設定） ---
    [System.Serializable]
    public class DoorGroupSettings
    {
        [Tooltip("Unityのインスペクター上で見やすくするためのグループ名（例: 正面玄関両開き、個室Aなど）")]
        public string groupName = "新しいドアグループ";

        [Tooltip("連動させたいドア（NewDoorMech）のリスト。2つ以上登録すると開閉が同期します")]
        public List<NewDoorMech> targetDoors = new List<NewDoorMech>();

        [Tooltip("ゲーム開始時にこのドアグループ全体に鍵をかけておくかどうか")]
        public bool isLocked = false;

        [Tooltip("このドアグループのロックを解除するために必要な固有の鍵ID（文字列）")]
        public string doorId = "Room_A_Key";

        // ─── 【内部用追加】動的に生成した障害物を管理するリスト ───
        [HideInInspector]
        public List<NavMeshObstacle> dynamicObstacles = new List<NavMeshObstacle>();
    }

    // --- 中間のリスト構造（部屋ごとのグループ化） ---
    [System.Serializable]
    public class RoomData
    {
        [Tooltip("Unityのインスペクター上で見やすくするための部屋の名前（例: 1F受付、美術室など）")]
        public string roomName = "新しい部屋";

        [Tooltip("この部屋の中に配置されているドアグループのリスト")]
        public List<DoorGroupSettings> doorGroups = new List<DoorGroupSettings>();
    }

    [Header("--- 外部システム参照 ---")]
    [Tooltip("全体の音関係を管理しているAudioManagerオブジェクトを割り当てます")]
    [SerializeField] private AudioManager audioManager;

    [Header("--- 部屋別・扉連動管理リスト（多重構造） ---")]
    [Tooltip("部屋ごとにグループ化して、すべてのドア連動グループを登録・管理します")]
    [SerializeField] private List<RoomData> roomDoorsList = new List<RoomData>();

    [Header("--- 鍵操作用オーディオ設定 (3D) ---")]
    [Tooltip("鍵がかかっていてドアが開かない時のガチャガチャ音")]
    [SerializeField] private AudioClip lockedSE;

    [Tooltip("鍵が正常に開いた時のカチャッという音")]
    [SerializeField] private AudioClip unlockSE;

    [SerializeField][Range(0f, 1f)] private float soundVolume = 0.8f;

    private AudioSource audioSource;

    void Start()
    {
        // 鍵音を操作ドアの場所から鳴らすためのAudioSource初期化
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.spatialBlend = 1.0f; // 3Dサウンド化
        audioSource.playOnAwake = false;

        // 初期設定のエラー確認、およびロック状態に応じたNavMeshObstacleの動的割り当て
        foreach (var room in roomDoorsList)
        {
            foreach (var group in room.doorGroups)
            {
                if (group.targetDoors.Count == 0)
                {
                    Debug.LogWarning($"【設定ミス注意】部屋 [{room.roomName}] のグループ [{group.groupName}] にドア（Target Doors）が1つも登録されていません。");
                }
                else
                {
                    for (int i = 0; i < group.targetDoors.Count; i++)
                    {
                        if (group.targetDoors[i] == null)
                        {
                            Debug.LogWarning($"【設定ミス注意】部屋 [{room.roomName}] のグループ [{group.groupName}] の要素 {i} に空の参照（Missing）があります。");
                        }
                        else
                        {
                            // ─── 【重要追加】NewDoorMechを変更せず、ここに動的にObstacleを生成する ───
                            // ゲーム開始時に鍵がかかっているドアの場合、敵の通行をカットする障害物を自動生成して貼り付ける
                            if (group.isLocked)
                            {
                                SetupDynamicObstacleForDoor(group, group.targetDoors[i]);
                            }
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// 【追加】ドアに対して動的にNavMeshObstacle（壁判定）を生成して登録するヘルパー関数
    /// </summary>
    private void SetupDynamicObstacleForDoor(DoorGroupSettings group, NewDoorMech door)
    {
        if (door == null) return;

        // ドアの親オブジェクト（またはドア自身）にNavMeshObstacleを動的にアタッチ
        NavMeshObstacle obstacle = door.gameObject.AddComponent<NavMeshObstacle>();

        // 敵が壁として認識するように設定を調整
        obstacle.carving = true;
        obstacle.carveOnlyStationary = true;

        // ドアのColliderのサイズに合わせて自動である程度形を合わせる（デフォルトでも機能しますが、調整用）
        BoxCollider box = door.GetComponentInChildren<BoxCollider>();
        if (box != null)
        {
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = door.transform.InverseTransformPoint(box.transform.transform.position) + box.center;
            obstacle.size = box.size;
        }

        // 管理リストに保存
        group.dynamicObstacles.Add(obstacle);
    }

    /// <summary>
    /// プレイヤーや環境がドアを操作しようとした時に呼び出すメインシステム（連動対応版）
    /// </summary>
    public void Interact(NewDoorMech interactedDoor, PlayerInventory playerInventory)
    {
        if (interactedDoor == null) return;

        DoorGroupSettings currentGroup = FindDoorGroup(interactedDoor);

        if (currentGroup == null)
        {
            interactedDoor.doorBool = !interactedDoor.doorBool;
            return;
        }

        transform.position = interactedDoor.transform.position;

        // 1. 鍵がかかっている場合の処理
        if (currentGroup.isLocked)
        {
            if (playerInventory != null && playerInventory.HasKey(currentGroup.doorId))
            {
                currentGroup.isLocked = false;
                PlaySound(unlockSE);
                Debug.Log($"【解錠成功】ドアグループ [{currentGroup.groupName}] をアンロックしました: {currentGroup.doorId}");

                if (GameMessageUI.Instance != null)
                {
                    GameMessageUI.Instance.ShowMessage("鍵を開けた", Color.white);
                }

                // ─── 【重要修正】解錠されたので、このグループに動的生成していたNavMeshObstacle（壁判定）をすべて消去する ───
                foreach (var obstacle in currentGroup.dynamicObstacles)
                {
                    if (obstacle != null)
                    {
                        Destroy(obstacle); // 障害物を消去し、敵が通れるようにする
                    }
                }
                currentGroup.dynamicObstacles.Clear();

                // 解錠と同時にグループ内のすべてのドアをオープン
                SetGroupDoorsState(currentGroup, true);
            }
            else
            {
                PlaySound(lockedSE);
                Debug.Log($"【解錠失敗】鍵が掛かっています。必要ID: {currentGroup.doorId}");

                if (GameMessageUI.Instance != null)
                {
                    GameMessageUI.Instance.ShowMessage("鍵がかかっている", Color.white);
                }
            }
        }
        // 2. 鍵がかかっていない（通常開閉）場合
        else
        {
            bool nextState = !interactedDoor.doorBool;
            SetGroupDoorsState(currentGroup, nextState);
        }
    }

    /// <summary>
    /// 外部（敵AIなど）からドアがロック中か尋ねられた際の確認用関数
    /// </summary>
    public bool IsDoorLocked(NewDoorMech targetDoor)
    {
        if (targetDoor == null) return false;

        DoorGroupSettings currentGroup = FindDoorGroup(targetDoor);
        if (currentGroup == null) return false;

        return currentGroup.isLocked;
    }

    private DoorGroupSettings FindDoorGroup(NewDoorMech targetDoor)
    {
        foreach (var room in roomDoorsList)
        {
            foreach (var group in room.doorGroups)
            {
                if (group.targetDoors.Contains(targetDoor))
                {
                    return group;
                }
            }
        }
        return null;
    }

    private void SetGroupDoorsState(DoorGroupSettings group, bool state)
    {
        foreach (var door in group.targetDoors)
        {
            if (door != null)
            {
                door.doorBool = state;
            }
        }
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null)
        {
            audioSource.PlayOneShot(clip, soundVolume);
        }
    }
}