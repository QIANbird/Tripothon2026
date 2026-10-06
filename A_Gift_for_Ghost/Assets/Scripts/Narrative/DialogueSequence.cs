using System.Collections.Generic;
using UnityEngine;

namespace Ghost.Narrative
{
    // 一段按顺序播放的对白，比如开场的三句。右键 Create → Ghost → Dialogue Sequence 新建
    [CreateAssetMenu(menuName = "Ghost/Dialogue Sequence", fileName = "DialogueSequence")]
    public class DialogueSequence : ScriptableObject
    {
        [Tooltip("开始播放前先等多久（秒）")]
        public float startDelay = 0f;
        public List<DialogueLine> lines = new List<DialogueLine>();
    }
}
