using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SpanishCultureCategorySO", menuName = "CleverMinds/Cultura general española/Question Category")]
public class SpanishCultureCategorySO : ScriptableObject
{
    [System.Serializable]
    public class SpanishCultureQuestionData
    {
        [TextArea(2, 5)]
        [SerializeField] private string questionText;
        [SerializeField] private Sprite questionImage;
        [SerializeField] private List<string> answers = new List<string>();
        [SerializeField] private int correctAnswerIndex = 0;

        public string QuestionText => questionText;
        public Sprite QuestionImage => questionImage;
        public List<string> Answers => answers;
        public int CorrectAnswerIndex => correctAnswerIndex;

        public bool IsValidForOptionsCount(int optionsCount)
        {
            if (answers == null) return false;
            if (answers.Count < optionsCount) return false;
            if (correctAnswerIndex < 0 || correctAnswerIndex >= answers.Count) return false;

            for (int i = 0; i < answers.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(answers[i]))
                    return false;
            }

            return !string.IsNullOrWhiteSpace(questionText);
        }
    }

    [Header("Datos de la categoría")]
    [SerializeField] private string categoryName;

    [Header("Preguntas de esta categoría")]
    [SerializeField] private List<SpanishCultureQuestionData> questions = new List<SpanishCultureQuestionData>();

    public string CategoryName => categoryName;
    public List<SpanishCultureQuestionData> Questions => questions;
}
