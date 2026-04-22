using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SONG_", menuName = "CleverMinds/MusicQuiz/Song Data")]
public class MusicQuizSongData : ScriptableObject
{
    [Header("Identidad")]
    public string songTitle;

    [Header("Audio (ya cortados por ti en producción)")]
    public AudioClip karaokeClip;          // Fragmento normal (con palabra)
    public AudioClip questionSilentClip;   // El MISMO fragmento pero con la palabra respuesta silenciada

    [Header("Karaoke (frases con tiempo)")]
    public List<KaraokeLine> lines = new();

    [Header("Pregunta")]
    [TextArea(2, 4)] public string questionText = "¿Qué palabra falta en la canción?";
    public string correctAnswer;

    [Tooltip("Incluye aquí opciones 'distractor'. El manager completará hasta 2/3/4 según nivel.")]
    public List<string> wrongAnswers = new();
}

[Serializable]
public class KaraokeLine
{
    [Tooltip("Segundo en el que empieza esta frase (desde el inicio del karaokeClip).")]
    public float startTime;

    [Tooltip("Texto de la frase que se muestra en pantalla.")]
    [TextArea(1, 3)]
    public string text;
}

