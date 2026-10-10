using Ghost.Narrative;

namespace Ghost.Agent.EditorTools
{
    // 构建场景时把聊天气泡素材挂到组件上（运行时不再走 AssetDatabase）
    public static class AgentChatArtLoader
    {
        public static void Assign(AgentChatFeed feed)
        {
            AgentChatArt.Ensure();
            feed.roundSprite = AgentUIStyle.LoadChatSprite(AgentUIStyle.ChatRoundSprite);
            feed.circleSprite = AgentUIStyle.LoadChatSprite(AgentUIStyle.ChatCircleSprite);
            feed.tailSprite = AgentUIStyle.LoadChatSprite(AgentUIStyle.ChatTailSprite);
            feed.headerSprite = AgentUIStyle.LoadChatSprite(AgentUIStyle.ChatHeaderSprite);
            feed.iconSprite = AgentUIStyle.LoadChatSprite(AgentUIStyle.ChatIconSprite);
        }
    }
}
