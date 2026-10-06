using System;
using System.Collections.Generic;
using Ghost.Morph;
using UnityEngine;

namespace Ghost.Gameplay
{
    public enum IssueDifficulty
    {
        // 点一次就解决
        Easy,
        // 要点 mediumClicks 次（默认 3）
        Medium,
        // 解决不了：点一次只暂停，过一会儿又开始闪烁
        Hard,
    }

    // 一个节点上的问题。由 NodeIssueSystem 创建和更新，外部只读
    [Serializable]
    public class NodeIssue
    {
        public int nodeId;
        public IssueDifficulty difficulty;
        // 还需要点几次才能解决；Hard 恒为 int.MaxValue
        public int clicksRemaining;
        // 一次尝试没解决时，过多久重新开始闪烁（秒）
        public float retryDelay;
        public bool resolved;
        // 累计尝试次数（S1 的困难问题按它判定"尝试 3 次以上"）
        public int attempts;

        // 正在等待重新闪烁（这段时间里点击不算尝试）
        public bool IsWaiting => !resolved && Time.time < retryAt;
        // 当前在闪烁，可以被点击尝试
        public bool IsActive => !resolved && !IsWaiting;

        [NonSerialized] internal float retryAt;
    }

    // 节点问题系统（第 4 节"通用机制"）：异常节点闪烁；点击 = 授权 Agent 尝试一次；
    // 成功停止闪烁，失败过一段时间重新闪烁。只管问题状态和节点表现，不读输入：点击由拾取层（G2 的 PointerInput）转交 TryAttempt。
    public class NodeIssueSystem : MonoBehaviour
    {
        public NodeMorpher morpher;

        [Header("难度")]
        [Tooltip("中等问题要点几次")]
        public int mediumClicks = 3;
        [Tooltip("中等问题每次没点完后，隔多久重新闪烁（秒）")]
        public float mediumRetryDelay = 0.6f;
        [Tooltip("困难问题每次尝试后暂停的时长范围（秒）")]
        public Vector2 hardRetryDelay = new Vector2(2f, 3f);

        [Header("表现")]
        [Tooltip("异常节点的闪烁频率（次/秒），0 = 用 NodeMorpher 的默认值")]
        public float blinkFrequency = 0f;
        [Tooltip("问题解决后短暂显示的恢复色")]
        public Color resolvedColor = new Color(0.45f, 0.75f, 0.95f);
        [Tooltip("恢复色显示多久后回到正常色（秒），0 = 一直保持恢复色")]
        public float resolvedTintDuration = 0.8f;

        // 新建问题时
        public event Action<NodeIssue> IssueCreated;
        // 每次有效点击（问题在闪烁时被点）。bool = 这次是否解决了
        public event Action<NodeIssue, bool> IssueAttempted;
        // 问题被解决（Easy 点一次，Medium 点够次数，或外部调用 Resolve）
        public event Action<NodeIssue> IssueResolved;

        readonly Dictionary<int, NodeIssue> issues = new Dictionary<int, NodeIssue>();
        readonly List<NodeIssue> ordered = new List<NodeIssue>();
        // 恢复色到期后要恢复正常显示的节点
        readonly Dictionary<int, float> tintUntil = new Dictionary<int, float>();
        readonly List<int> expired = new List<int>();

        public IReadOnlyList<NodeIssue> Issues => ordered;

        void Reset()
        {
            morpher = GetComponent<NodeMorpher>();
        }

        void Awake()
        {
            if (morpher == null) morpher = GetComponent<NodeMorpher>();
        }

        public NodeIssue GetIssue(int nodeId)
        {
            return issues.TryGetValue(nodeId, out var issue) ? issue : null;
        }

        public bool HasIssue(int nodeId)
        {
            return issues.ContainsKey(nodeId);
        }

        // 在指定节点上建问题。节点已有问题时替换。返回 null 表示节点 id 无效
        public NodeIssue AddIssue(int nodeId, IssueDifficulty difficulty)
        {
            if (morpher == null || morpher.nodeSet == null || morpher.nodeSet.Get(nodeId) == null) return null;
            RemoveIssue(nodeId);

            var issue = new NodeIssue
            {
                nodeId = nodeId,
                difficulty = difficulty,
                clicksRemaining = ClicksFor(difficulty),
                retryDelay = difficulty == IssueDifficulty.Hard ? hardRetryDelay.x : mediumRetryDelay,
            };
            issues[nodeId] = issue;
            ordered.Add(issue);
            tintUntil.Remove(nodeId);
            morpher.SetBlink(nodeId, blinkFrequency);
            IssueCreated?.Invoke(issue);
            return issue;
        }

        // 一组节点都建同一难度的问题（比如 S1 的 4 个果实节点 = 困难）
        public List<NodeIssue> AddIssues(IEnumerable<int> nodeIds, IssueDifficulty difficulty)
        {
            var result = new List<NodeIssue>();
            foreach (int id in nodeIds)
            {
                var issue = AddIssue(id, difficulty);
                if (issue != null) result.Add(issue);
            }
            return result;
        }

        // 按规则随机挑 count 个还没有问题的节点建问题。filter 为空时可以选任意节点。seed < 0 时每次随机
        public List<NodeIssue> GenerateRandom(int count, IssueDifficulty difficulty, Predicate<PlantNode> filter = null, int seed = -1)
        {
            var candidates = new List<int>();
            if (morpher != null && morpher.nodeSet != null)
            {
                foreach (var node in morpher.nodeSet.nodes)
                    if (!issues.ContainsKey(node.id) && (filter == null || filter(node))) candidates.Add(node.id);
            }

            var rng = seed >= 0 ? new System.Random(seed) : new System.Random();
            var picked = new List<int>();
            for (int i = 0; i < count && candidates.Count > 0; i++)
            {
                int k = rng.Next(candidates.Count);
                picked.Add(candidates[k]);
                candidates.RemoveAt(k);
            }
            if (picked.Count < count)
                Debug.LogWarning($"[Issue] 只找到 {picked.Count} 个符合条件的节点，要求 {count} 个", this);
            return AddIssues(picked, difficulty);
        }

        // 点击节点 = 授权 Agent 尝试解决一次。返回 true 表示这次点击被问题系统处理了（节点上有正在闪烁的问题）
        public bool TryAttempt(int nodeId)
        {
            if (!issues.TryGetValue(nodeId, out var issue) || !issue.IsActive) return false;

            issue.attempts++;
            bool solved = false;
            switch (issue.difficulty)
            {
                case IssueDifficulty.Easy:
                    solved = true;
                    break;
                case IssueDifficulty.Medium:
                    issue.clicksRemaining--;
                    solved = issue.clicksRemaining <= 0;
                    break;
                case IssueDifficulty.Hard:
                    issue.retryDelay = UnityEngine.Random.Range(hardRetryDelay.x, hardRetryDelay.y);
                    break;
            }

            if (solved)
            {
                MarkResolved(issue);
                IssueAttempted?.Invoke(issue, true);
                IssueResolved?.Invoke(issue);
            }
            else
            {
                // 没解决：暂停闪烁，等 retryDelay 后重新闪烁
                issue.retryAt = Time.time + issue.retryDelay;
                morpher.Restore(nodeId);
                IssueAttempted?.Invoke(issue, false);
            }
            return true;
        }

        // 不经点击直接解决（比如 S2 拖拽经过的节点）。Hard 也可以被这样解决
        public bool Resolve(int nodeId)
        {
            if (!issues.TryGetValue(nodeId, out var issue) || issue.resolved) return false;
            MarkResolved(issue);
            IssueResolved?.Invoke(issue);
            return true;
        }

        // 去掉节点上的问题并恢复正常显示，不发事件
        public void RemoveIssue(int nodeId)
        {
            if (!issues.TryGetValue(nodeId, out var issue)) return;
            issues.Remove(nodeId);
            ordered.Remove(issue);
            tintUntil.Remove(nodeId);
            if (morpher != null) morpher.Restore(nodeId);
        }

        public void ClearAll()
        {
            foreach (var issue in ordered)
                if (morpher != null) morpher.Restore(issue.nodeId);
            issues.Clear();
            ordered.Clear();
            tintUntil.Clear();
        }

        // 某种难度（为空 = 全部）的问题是否都解决了
        public bool AllResolved(IssueDifficulty? difficulty = null)
        {
            foreach (var issue in ordered)
                if ((difficulty == null || issue.difficulty == difficulty) && !issue.resolved) return false;
            return true;
        }

        public int CountUnresolved(IssueDifficulty? difficulty = null)
        {
            int n = 0;
            foreach (var issue in ordered)
                if ((difficulty == null || issue.difficulty == difficulty) && !issue.resolved) n++;
            return n;
        }

        // 某种难度的问题累计被尝试了几次（S1：困难问题累计尝试 3 次以上）
        public int TotalAttempts(IssueDifficulty? difficulty = null)
        {
            int n = 0;
            foreach (var issue in ordered)
                if (difficulty == null || issue.difficulty == difficulty) n += issue.attempts;
            return n;
        }

        void Update()
        {
            float now = Time.time;
            foreach (var issue in ordered)
            {
                // 等待结束：重新闪烁
                if (!issue.resolved && issue.retryAt > 0f && now >= issue.retryAt)
                {
                    issue.retryAt = 0f;
                    morpher.SetBlink(issue.nodeId, blinkFrequency);
                }
            }

            if (tintUntil.Count == 0) return;
            expired.Clear();
            foreach (var pair in tintUntil)
                if (now >= pair.Value) expired.Add(pair.Key);
            foreach (int id in expired)
            {
                tintUntil.Remove(id);
                morpher.Restore(id);
            }
        }

        void MarkResolved(NodeIssue issue)
        {
            issue.resolved = true;
            issue.retryAt = 0f;
            issue.clicksRemaining = 0;
            morpher.SetTint(issue.nodeId, resolvedColor);
            if (resolvedTintDuration > 0f) tintUntil[issue.nodeId] = Time.time + resolvedTintDuration;
        }

        int ClicksFor(IssueDifficulty difficulty)
        {
            switch (difficulty)
            {
                case IssueDifficulty.Easy: return 1;
                case IssueDifficulty.Medium: return Mathf.Max(1, mediumClicks);
                default: return int.MaxValue;
            }
        }
    }
}
