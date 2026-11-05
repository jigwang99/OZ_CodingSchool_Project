using System;
using UnityEngine;

namespace AutoBattler.Presentation.Battle
{
    [Serializable]
    public sealed class RoundProgressSprites
    {
        [SerializeField] private Sprite pending;
        [SerializeField] private Sprite current;
        [SerializeField] private Sprite win;
        [SerializeField] private Sprite loss;
        public Sprite Pending => pending;
        public Sprite Current => current;
        public Sprite Win => win;
        public Sprite Loss => loss;
    }

    [CreateAssetMenu(menuName="PixelTactics/Round Progress Settings")]
    public sealed class RoundProgressSettings : ScriptableObject
    {
        [SerializeField] private RoundProgressSprites combat;
        [SerializeField] private RoundProgressSprites boss;
        public RoundProgressSprites Combat => combat;
        public RoundProgressSprites Boss => boss;
    }
}
