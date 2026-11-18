using System;
using UnityEngine;

namespace AutoBattler.Presentation.Units
{
    [Serializable]
    public sealed class SkillEffectSequence
    {
        [SerializeField] private Sprite[] frames = Array.Empty<Sprite>();
        [SerializeField, Min(1)] private int framesPerSecond = 24;
        [SerializeField, Min(.01f)] private float scale = 1f;
        [SerializeField] private bool directional;

        public bool HasFrames => frames.Length > 0 && frames[0] != null;
        public float Duration => (float)frames.Length / framesPerSecond;
        public float Scale => scale;
        public bool Directional => directional;

        public Sprite FrameAt(float elapsed, bool loop)
        {
            int index = (int)(elapsed * framesPerSecond);
            return frames[loop ? index % frames.Length : Mathf.Min(index, frames.Length - 1)];
        }
    }

    [Serializable]
    public sealed class SkillEffectStyle
    {
        [SerializeField] private int skillId;
        [SerializeField] private SkillEffectSequence impact;
        [SerializeField] private SkillEffectSequence projectile;

        public int SkillId => skillId;
        public SkillEffectSequence Impact => impact != null && impact.HasFrames ? impact : null;
        public SkillEffectSequence Projectile => projectile != null && projectile.HasFrames ? projectile : null;
    }

    /// <summary>스킬과 유닛 승급의 픽셀 이펙트 프레임과 재생 설정.</summary>
    [CreateAssetMenu(menuName = "PixelTactics/Skill Effect Library")]
    public sealed class SkillEffectLibrary : ScriptableObject
    {
        [SerializeField] private SkillEffectStyle[] styles = Array.Empty<SkillEffectStyle>();
        [SerializeField] private SkillEffectSequence twoStarEffect;
        [SerializeField] private SkillEffectSequence threeStarEffect;

        public SkillEffectSequence Promotion(int star)
        {
            SkillEffectSequence effect = star switch
            {
                2 => twoStarEffect,
                3 => threeStarEffect,
                _ => null
            };
            return effect != null && effect.HasFrames ? effect : null;
        }

        public SkillEffectStyle GetStyle(int skillId)
        {
            foreach (SkillEffectStyle style in styles)
                if (style.SkillId == skillId) return style;
            return null;
        }
    }
}
