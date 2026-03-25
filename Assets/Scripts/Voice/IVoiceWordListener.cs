/// <summary>
/// Интерфейс слушателя распознанных слов.
/// Реализуйте этот интерфейс и зарегистрируйте слушателя в VoiceRecognizer,
/// чтобы получать все распознанные слова из речи.
/// </summary>
public interface IVoiceWordListener
{
    /// <summary>
    /// Вызывается при распознавании слова.
    /// </summary>
    /// <param name="word">Распознанное слово.</param>
    void OnWordRecognized(string word);
}
