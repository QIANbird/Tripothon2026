using Ghost.Agent;
using Ghost.Core;
using Ghost.Interaction;
using Ghost.Morph;
using Ghost.Narrative;
using Ghost.Pick;
using Ghost.Stages;
using UnityEngine;

namespace Ghost.Audio
{
    // 把玩法事件接到音效上（编号见 05_audio_list.xlsx 的"音效"页）。只订阅事件、读状态，不改玩法。
    // 引用都从 GameFlow / StageContext 里找，场景里由 AudioSceneBuilder 挂在 GameAudio 上。
    // 还没交付的编号由 GameAudio 跳过，交付后放进 Assets/Audio/SFX 就会响，不用改这里。
    [RequireComponent(typeof(GameAudio))]
    public class GameAudioCues : MonoBehaviour
    {
        [Tooltip("S2 连上段内第 n 个节点时的音高 = 1 + step × (n - 1)")]
        public float linkPitchStep = 0.06f;
        public float linkPitchMax = 1.5f;

        GameAudio audioOut;
        GameFlow flow;
        StageContext ctx;
        AgentChatFeed feed;
        TutorialStage tutorial;
        S2CircuitStage s2;
        S3NetworkStage s3;
        S4GeometricStage s4;
        PickStage pick;

        void Awake()
        {
            audioOut = GetComponent<GameAudio>();
            flow = audioOut.flow;
            if (flow == null) return;
            ctx = flow.GetComponent<StageContext>();
            if (ctx != null && ctx.subtitles != null) feed = ctx.subtitles.agentFeed;
            if (flow.stages == null) return;
            foreach (var stage in flow.stages)
            {
                if (stage is TutorialStage t) tutorial = t;
                else if (stage is S2CircuitStage a) s2 = a;
                else if (stage is S3NetworkStage b) s3 = b;
                else if (stage is S4GeometricStage c) s4 = c;
                else if (stage is PickStage p) pick = p;
            }
        }

        void OnEnable()
        {
            if (flow != null) flow.StageExited += HandleStageExited;
            if (ctx != null)
            {
                if (ctx.pointer != null) ctx.pointer.Tap += HandleTap;
                if (ctx.morpher != null) ctx.morpher.MorphStarted += HandleMorphStarted;
                if (ctx.query != null) ctx.query.Opened += HandleQueryOpened;
            }
            if (feed != null) feed.Pushed += HandleAgentPushed;
            if (tutorial != null) tutorial.MergeStarted += HandleTutorialMerge;
            if (s2 != null)
            {
                s2.TraceStarted += HandleTraceStarted;
                s2.TraceEnded += HandleTraceEnded;
                s2.NodeLinked += HandleNodeLinked;
            }
            if (s3 != null) s3.BugMarked += HandleBugMarked;
            if (s4 != null) s4.BugTaken += HandleBugTaken;
            if (pick != null)
            {
                if (pick.hand != null) pick.hand.Picked += HandleFruitPicked;
                if (pick.eat != null) pick.eat.BiteTaken += HandleBite;
            }
        }

        void OnDisable()
        {
            if (flow != null) flow.StageExited -= HandleStageExited;
            if (ctx != null)
            {
                if (ctx.pointer != null) ctx.pointer.Tap -= HandleTap;
                if (ctx.morpher != null) ctx.morpher.MorphStarted -= HandleMorphStarted;
                if (ctx.query != null) ctx.query.Opened -= HandleQueryOpened;
            }
            if (feed != null) feed.Pushed -= HandleAgentPushed;
            if (tutorial != null) tutorial.MergeStarted -= HandleTutorialMerge;
            if (s2 != null)
            {
                s2.TraceStarted -= HandleTraceStarted;
                s2.TraceEnded -= HandleTraceEnded;
                s2.NodeLinked -= HandleNodeLinked;
            }
            if (s3 != null) s3.BugMarked -= HandleBugMarked;
            if (s4 != null) s4.BugTaken -= HandleBugTaken;
            if (pick != null)
            {
                if (pick.hand != null) pick.hand.Picked -= HandleFruitPicked;
                if (pick.eat != null) pick.eat.BiteTaken -= HandleBite;
            }
            if (audioOut != null) audioOut.StopAllLoops();
        }

        // 持续状态的循环音：Agent 打字声跟着聊天栏的打字机；S4 拖拽旋转时的叶子沙沙声
        void Update()
        {
            SetLoop("SFX_AGENT_TYPE", feed != null && feed.IsRevealing);
            bool rustle = s4 != null && s4.IsActive && s4.rotator != null && s4.rotator.IsDragging;
            SetLoop("SFX_S4_LEAF_RUSTLE", rustle);
        }

        void SetLoop(string id, bool on)
        {
            if (on) audioOut.StartLoop(id);
            else if (audioOut.IsLooping(id)) audioOut.StopLoop(id);
        }

        void HandleStageExited(Stage stage)
        {
            // 跳关时拖拽可能还没结束，拖拽声不能留到下一关
            if (stage == s2) audioOut.StopLoop("SFX_S2_DRAG");
        }

        // 左键点节点 = 授权 Agent 处理一次（教学、S1）
        void HandleTap(int id)
        {
            if (flow.CurrentStage is IssueStage) audioOut.PlayOneShot("SFX_TAP_AUTH");
        }

        // 每次变形一个变体：_01 矩阵→回路，_02 回路→网络，_03 网络→几何，_04 几何→写实（还没交付）
        void HandleMorphStarted(MorphForm form)
        {
            int index;
            switch (form)
            {
                case MorphForm.Circuit: index = 0; break;
                case MorphForm.Network: index = 1; break;
                case MorphForm.Geometric: index = 2; break;
                case MorphForm.Real: index = 3; break;
                default: return; // 回到矩阵（跳关）不出声
            }
            audioOut.PlayVariant("SFX_MORPH", index);
        }

        // S3 的询问是高风险警告，其他关是普通询问
        void HandleQueryOpened(string question)
        {
            audioOut.PlayOneShot(flow.CurrentStage == s3 && s3 != null ? "SFX_QUERY_WARN" : "SFX_QUERY_OPEN");
        }

        // Agent 弹窗出现。S1 点方块的随机池（S1_P*）没有配音，用数据念白代替
        void HandleAgentPushed(AgentChatFeed.Kind kind)
        {
            var line = ctx != null && ctx.dialogue != null ? ctx.dialogue.CurrentLine : null;
            bool pool = line != null && line.id != null && line.id.StartsWith("S1_P");
            audioOut.PlayOneShot(pool ? "SFX_AGENT_DATA" : "SFX_AGENT_POPUP");
        }

        void HandleTutorialMerge() => audioOut.PlayOneShot("SFX_TUT_TO_MATRIX");

        void HandleTraceStarted() => audioOut.StartLoop("SFX_S2_DRAG");
        void HandleTraceEnded() => audioOut.StopLoop("SFX_S2_DRAG");

        void HandleNodeLinked(int indexInSegment)
        {
            float pitch = Mathf.Min(linkPitchMax, 1f + linkPitchStep * Mathf.Max(0, indexInSegment - 1));
            audioOut.PlayPitched("SFX_S2_NODE_LINK", pitch);
        }

        void HandleBugMarked() => audioOut.PlayOneShot("SFX_S3_MARK");
        void HandleBugTaken(int removed, int total) => audioOut.PlayOneShot("SFX_S4_REMOVE_BUG");
        void HandleFruitPicked(PickableFruit fruit) => audioOut.PlayOneShot("SFX_PICK_SNAP");
        void HandleBite(int index, int total) => audioOut.PlayOneShot("SFX_BITE");
    }
}
