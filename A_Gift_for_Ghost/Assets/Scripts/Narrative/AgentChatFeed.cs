using System.Collections.Generic;
using Ghost.Agent;
using UnityEngine;
using UnityEngine.UI;

namespace Ghost.Narrative
{
    // 左侧 Agent 聊天栏：每句 Agent 台词是一条新消息，从底部滑入、向下堆叠。
    // 最上面那条在它的台词播完 3 s 后淡出，下面的条目平滑上移。询问框不进这里。
    // 不读输入、不读屏幕像素，位置都是 Canvas 锚点坐标。
    public class AgentChatFeed : MonoBehaviour
    {
        public enum Kind { Chat, Caution }

        [Header("引用（NarrativeSceneBuilder 会填好）")]
        public RectTransform feedRoot;     // 左上锚定的栏，高度随条目总高变化
        public RectTransform itemParent;   // 条目挂在这里

        [Header("素材（构建时填好；缺图退回直角纯色块）")]
        public Sprite roundSprite;
        public Sprite circleSprite;
        public Sprite tailSprite;
        public Sprite headerSprite;
        public Sprite iconSprite;

        [Header("尺寸")]
        public float width = AgentUIStyle.HudLeftColumnWidth;
        public float avatarSize = 56f;
        public float avatarGap = 12f;
        public float itemGap = 12f;
        public float bubblePadH = 18f;
        public float bubblePadV = 14f;
        public float cautionHeaderHeight = 52f;
        public int chatFontSize = 24;
        public int cautionTitleSize = 22;
        public int cautionBodySize = 24;
        public int maxVisible = 8;

        [Header("动画")]
        public float enterSeconds = 0.2f;
        public float enterOffset = 24f;
        public float fadeSeconds = 0.25f;
        [Tooltip("最上面那条：它的台词播完后过这么久才淡出")]
        public float expireAfterFinished = 3f;

        public bool HasItems => items.Count > 0;
        // 最新一条还在打字（字幕面板用来决定 Advance 是补全还是跳句）
        public bool IsRevealing => revealing && revealingItem != null;
        // 栏的当前总高度（详情弹窗 stackBelow 用 feedRoot.rect.height）
        public float CurrentHeight => feedRoot != null ? feedRoot.rect.height : 0f;

        class Item
        {
            public Kind kind;
            public bool pinned;
            public bool finished;
            public float finishedAt = -1f;
            public float height;
            public float targetY;          // 相对 itemParent 左上，向下为负
            public float visualY;
            public float enterT;
            public float fadeT = 1f;       // 1 = 完全可见，0 = 该销毁
            public bool fading;
            public string fullText = "";
            public RectTransform root;
            public CanvasGroup group;
            public Text body;
        }

        readonly List<Item> items = new List<Item>();
        Item revealingItem;
        string revealText = "";
        float revealStart;
        bool revealing;
        public float typewriterCharsPerSecond = 30f;

        void Update()
        {
            TickTypewriter();
            ExpireTop();
            Animate();
        }

        // 追加一条。kind 由通道决定（AgentCaution → Caution，其余 Agent 通道 → Chat）
        public void Push(Kind kind, string text)
        {
            if (itemParent == null) return;
            var item = Build(kind, text ?? "", pinned: false);
            items.Add(item);
            revealingItem = item;
            revealText = item.fullText;
            revealing = typewriterCharsPerSecond > 0f && revealText.Length > 0;
            revealStart = Time.time;
            if (item.body != null) item.body.text = revealing ? "" : revealText;
            TrimOverflow();
            Relayout();
            item.visualY = item.targetY - enterOffset;
        }

        // 台词播完：当前这条开始计 3 s（还没淡出）
        public void MarkFinished()
        {
            revealing = false;
            if (revealingItem == null) return;
            if (revealingItem.body != null) revealingItem.body.text = revealingItem.fullText;
            revealingItem.finished = true;
            revealingItem.finishedAt = Time.time;
            revealingItem = null;
        }

        public void CompleteReveal()
        {
            if (!revealing || revealingItem == null) return;
            revealing = false;
            if (revealingItem.body != null) revealingItem.body.text = revealingItem.fullText;
        }

        // 固定一条聊天气泡在栏底。再次调用只改文字，不追加。
        public void Pin(string text)
        {
            var existing = FindPinned();
            if (existing != null)
            {
                existing.fullText = text ?? "";
                if (existing.body != null) existing.body.text = existing.fullText;
                Measure(existing);
                Relayout();
                return;
            }
            var item = Build(Kind.Chat, text ?? "", pinned: true);
            if (item.body != null) item.body.text = item.fullText;
            item.finished = true;
            items.Add(item);
            TrimOverflow();
            Relayout();
            item.visualY = item.targetY - enterOffset;
        }

        public void Unpin()
        {
            var existing = FindPinned();
            if (existing == null) return;
            existing.fading = true;
        }

        // 跳关 / 被打断：立刻清空，不做动画。keepPinned 时固定消息留着
        public void ClearImmediate(bool keepPinned = false)
        {
            revealing = false;
            revealingItem = null;
            for (int i = items.Count - 1; i >= 0; i--)
            {
                var item = items[i];
                if (keepPinned && item.pinned && !item.fading) continue;
                if (item.root != null) Destroy(item.root.gameObject);
                items.RemoveAt(i);
            }
            Relayout();
        }

        Item FindPinned()
        {
            for (int i = 0; i < items.Count; i++)
                if (items[i].pinned && !items[i].fading) return items[i];
            return null;
        }

        void TickTypewriter()
        {
            if (!revealing || revealingItem == null || revealingItem.body == null) return;
            int count = Mathf.FloorToInt((Time.time - revealStart) * typewriterCharsPerSecond);
            if (count >= revealText.Length)
            {
                revealingItem.body.text = revealText;
                revealing = false;
            }
            else revealingItem.body.text = revealText.Substring(0, Mathf.Max(0, count));
        }

        // 只有最上面那条（非 Pin、非正在淡出）会过期；已经超时的下一条立刻接着淡出
        void ExpireTop()
        {
            Item top = null;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].fading || items[i].pinned) continue;
                top = items[i];
                break;
            }
            if (top == null || !top.finished) return;
            if (Time.time - top.finishedAt >= expireAfterFinished) top.fading = true;
        }

        void TrimOverflow()
        {
            int living = 0;
            for (int i = 0; i < items.Count; i++)
                if (!items[i].fading) living++;
            if (living <= maxVisible) return;
            for (int i = 0; i < items.Count && living > maxVisible; i++)
            {
                if (items[i].fading || items[i].pinned) continue;
                items[i].fading = true;
                living--;
            }
        }

        void Relayout()
        {
            float y = 0f;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item.root == null) continue;
                item.targetY = -y;
                y += item.height + itemGap;
            }
            float total = y > 0f ? y - itemGap : 0f;
            if (feedRoot != null) feedRoot.sizeDelta = new Vector2(width, total);
        }

        void Animate()
        {
            float dt = Time.deltaTime;
            bool anyGone = false;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item.root == null) { anyGone = true; continue; }

                if (item.fading)
                {
                    item.fadeT = fadeSeconds > 0f ? Mathf.MoveTowards(item.fadeT, 0f, dt / fadeSeconds) : 0f;
                    if (item.fadeT <= 0f)
                    {
                        Destroy(item.root.gameObject);
                        item.root = null;
                        anyGone = true;
                        continue;
                    }
                }

                item.enterT = enterSeconds > 0f ? Mathf.MoveTowards(item.enterT, 1f, dt / enterSeconds) : 1f;
                float enter = EaseOut(item.enterT);
                float shownY = Mathf.Lerp(item.visualY, item.targetY, 1f - Mathf.Exp(-14f * dt));
                item.visualY = shownY;
                item.root.anchoredPosition = new Vector2(0f, shownY - (1f - enter) * enterOffset);
                if (item.group != null) item.group.alpha = item.fadeT * enter;
            }

            if (!anyGone) return;
            for (int i = items.Count - 1; i >= 0; i--)
                if (items[i].root == null) items.RemoveAt(i);
            Relayout();
        }

        static float EaseOut(float t) => 1f - (1f - t) * (1f - t);

        Item Build(Kind kind, string text, bool pinned)
        {
            var item = new Item { kind = kind, pinned = pinned, fullText = text, enterT = 0f };
            item.root = AgentUIStyle.CreateRect(kind == Kind.Caution ? "Caution" : "Chat", itemParent, Vector2.zero,
                new Vector2(width, 80f));
            item.group = item.root.gameObject.AddComponent<CanvasGroup>();
            item.group.blocksRaycasts = false;
            item.group.interactable = false;
            item.group.alpha = 0f;
            if (kind == Kind.Caution) BuildCaution(item);
            else BuildChat(item);
            Measure(item);
            return item;
        }

        void BuildChat(Item item)
        {
            var avatar = AgentUIStyle.CreateRect("Avatar", item.root, Vector2.zero, new Vector2(avatarSize, avatarSize));
            AgentUIStyle.AddSprite(avatar, circleSprite, AgentUIStyle.ChatAvatar);
            var icon = AgentUIStyle.CreateStretch("Icon", avatar, avatarSize * 0.22f);
            AgentUIStyle.AddSprite(icon, iconSprite, AgentUIStyle.ChatIcon);

            float bubbleX = avatarSize + avatarGap;
            float bubbleW = width - bubbleX;
            var bubble = AgentUIStyle.CreateRect("Bubble", item.root, new Vector2(bubbleX, 0f), new Vector2(bubbleW, 80f));
            bubble.name = "Bubble";
            AgentUIStyle.AddSprite(bubble, roundSprite, AgentUIStyle.ChatBubble, sliced: true);

            if (tailSprite != null)
            {
                var tail = AgentUIStyle.CreateRect("Tail", item.root, new Vector2(bubbleX - 10f, -18f), new Vector2(18f, 16f));
                AgentUIStyle.AddSprite(tail, tailSprite, AgentUIStyle.ChatBubble);
            }

            var bodyRect = AgentUIStyle.CreateRect("Text", bubble, new Vector2(bubblePadH, -bubblePadV),
                new Vector2(bubbleW - bubblePadH * 2f, 40f));
            item.body = AgentUIStyle.AddText(bodyRect, "", chatFontSize, AgentUIStyle.ChatText, TextAnchor.UpperLeft);
            item.body.lineSpacing = 1.1f;
        }

        void BuildCaution(Item item)
        {
            var card = AgentUIStyle.CreateRect("Card", item.root, Vector2.zero, new Vector2(width, 80f));
            card.name = "Card";
            AgentUIStyle.AddSprite(card, roundSprite, AgentUIStyle.CautionBody, sliced: true);

            // 顶部条：矩形色块，盖住卡片上半圆角；下沿被卡片挡住，看起来像标签卡
            var header = AgentUIStyle.CreateRect("Header", card, Vector2.zero, new Vector2(width, cautionHeaderHeight));
            AgentUIStyle.AddSprite(header, headerSprite != null ? headerSprite : roundSprite, AgentUIStyle.CautionHeader, sliced: true);
            float a = cautionHeaderHeight - 8f;
            var avatar = AgentUIStyle.CreateRect("Avatar", header, new Vector2(4f, -4f), new Vector2(a, a));
            AgentUIStyle.AddSprite(avatar, circleSprite, AgentUIStyle.ChatAvatar);
            var icon = AgentUIStyle.CreateStretch("Icon", avatar, a * 0.22f);
            AgentUIStyle.AddSprite(icon, iconSprite, AgentUIStyle.ChatIcon);

            var title = AgentUIStyle.CreateRect("Title", header, new Vector2(a + 16f, 0f),
                new Vector2(width - a - 32f, cautionHeaderHeight));
            AgentUIStyle.AddText(title, "Caution", cautionTitleSize, AgentUIStyle.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);

            var bodyRect = AgentUIStyle.CreateRect("Text", card, new Vector2(bubblePadH, -(cautionHeaderHeight + 10f)),
                new Vector2(width - bubblePadH * 2f, 40f));
            item.body = AgentUIStyle.AddText(bodyRect, "", cautionBodySize, AgentUIStyle.Ink, TextAnchor.UpperLeft);
            item.body.lineSpacing = 1.1f;
        }

        void Measure(Item item)
        {
            if (item.body == null) return;
            string shown = item.body.text;
            item.body.text = item.fullText;
            float bodyH = Mathf.Max(item.body.fontSize * 1.2f, item.body.preferredHeight);
            item.body.text = shown;
            item.body.rectTransform.sizeDelta = new Vector2(item.body.rectTransform.sizeDelta.x, bodyH);

            float height;
            if (item.kind == Kind.Caution)
            {
                height = cautionHeaderHeight + 10f + bodyH + bubblePadV + 4f;
                var card = item.root.Find("Card") as RectTransform;
                if (card != null) card.sizeDelta = new Vector2(width, height);
            }
            else
            {
                float bubbleH = bodyH + bubblePadV * 2f;
                height = Mathf.Max(avatarSize, bubbleH);
                var bubble = item.root.Find("Bubble") as RectTransform;
                if (bubble != null) bubble.sizeDelta = new Vector2(bubble.sizeDelta.x, bubbleH);
            }
            item.height = height;
            item.root.sizeDelta = new Vector2(width, height);
        }
    }
}
