using System;
using System.Collections.Generic;
using UnityEngine;

namespace Witch.Infrastructure.Content
{
    [CreateAssetMenu(menuName = "Witch/Content/Witch Hunter Dialogue", fileName = "WitchHunterDialogue")]
    public sealed class WitchHunterDialogueDefinition : StableContentDefinition
    {
        public const string ContentId = "dialogue.witchHunter";

        [Serializable]
        public sealed class Stage
        {
            [SerializeField, TextArea(2, 4)] private string[] accusations = Array.Empty<string>();
            [SerializeField, TextArea(2, 4)] private string successLine;

            public IReadOnlyList<string> Accusations => accusations;
            public string SuccessLine => successLine;

            public void Configure(string[] accusationValues, string successValue)
            {
                accusations = accusationValues == null
                    ? Array.Empty<string>()
                    : (string[])accusationValues.Clone();
                successLine = successValue;
            }
        }

        [SerializeField] private Stage[] stages = Array.Empty<Stage>();

        public int StageCount => stages.Length;

        public IReadOnlyList<string> GetAccusations(int stageIndex)
        {
            return GetStage(stageIndex).Accusations;
        }

        public string GetSuccessLine(int stageIndex)
        {
            return GetStage(stageIndex).SuccessLine;
        }

        public void Configure(Stage[] stageValues)
        {
            SetStableId(ContentId);
            stages = stageValues == null ? Array.Empty<Stage>() : (Stage[])stageValues.Clone();
        }

        private Stage GetStage(int stageIndex)
        {
            if (stageIndex < 0 || stageIndex >= stages.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(stageIndex));
            }

            return stages[stageIndex]
                   ?? throw new InvalidOperationException(
                       "Witch hunter dialogue stage " + (stageIndex + 1) + " is missing.");
        }
    }
}
