using System;

namespace SM.Content.Definitions
{
    [Serializable]
    public sealed class DropGradeProfileDefinition
    {
        public string ChapterId = string.Empty;
        public float InitialLatentMean;
        public float InitialStandardDeviation = 0.78f;
        public float MeanPreservingLatentMean;
        public float StandardDeviation = 0.78f;
    }
}
