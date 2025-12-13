using UnityEngine;
using System.Collections.Generic;

namespace BossFight2D.Systems
{
    [System.Serializable]
    public class QuestionData
    {
        public int id;
        public string topic;
        public string difficulty;
        public string prompt;
        public string[] options;
        public int correctIndex;
        public int timeLimitSec = 20;
        public string explanation;
    }

    [System.Serializable]
    public class QuestionPackData
    {
        public string name;
        public List<QuestionData> questions = new();
    }

    [System.Serializable]
    public class Wrapper
    {
        public QuestionPackData pack;
    }
}